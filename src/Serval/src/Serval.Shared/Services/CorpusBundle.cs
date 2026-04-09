namespace Serval.Shared.Services;

public class CorpusBundle
{
    private readonly Dictionary<
        string,
        (ParatextProjectSettings DaughterSettings, string? ParentLocation, ParatextProjectSettings? ParentSettings)
    > _settings;

    public IEnumerable<(
        ParallelCorpusContract ParallelCorpus,
        MonolingualCorpusContract MonolingualCorpus,
        IReadOnlyList<CorpusFileContract> CorpusFile,
        IReadOnlyList<ITextCorpus> TextCorpora
    )> SourceTextCorpora { get; }

    public IEnumerable<(
        ParallelCorpusContract ParallelCorpus,
        MonolingualCorpusContract MonolingualCorpus,
        IReadOnlyList<CorpusFileContract> CorpusFile,
        IReadOnlyList<ITextCorpus> TextCorpora
    )> TargetTextCorpora { get; }

    public IEnumerable<(
        ParallelCorpusContract ParallelCorpus,
        MonolingualCorpusContract MonolingualCorpus,
        IReadOnlyList<CorpusFileContract> CorpusFile,
        IReadOnlyList<ITextCorpus> TextCorpora
    )> TextCorpora => SourceTextCorpora.Concat(TargetTextCorpora);

    public IEnumerable<(
        ParallelCorpusContract ParallelCorpus,
        MonolingualCorpusContract MonolingualCorpus,
        IReadOnlyList<CorpusFileContract> CorpusFile,
        IReadOnlyList<ITextCorpus> TextCorpora
    )> SourceTermCorpora { get; }

    public IEnumerable<(
        ParallelCorpusContract ParallelCorpus,
        MonolingualCorpusContract MonolingualCorpus,
        IReadOnlyList<CorpusFileContract> CorpusFile,
        IReadOnlyList<ITextCorpus> TextCorpora
    )> TargetTermCorpora { get; }
    public IReadOnlyList<ParallelCorpusContract> ParallelCorpora { get; }

    public CorpusBundle(IEnumerable<ParallelCorpusContract> parallelCorpora)
    {
        ParallelCorpora = parallelCorpora.ToArray();

        _settings = [];
        IEnumerable<CorpusFileContract> corpusFiles = parallelCorpora.SelectMany(corpus =>
            corpus.SourceCorpora.Concat(corpus.TargetCorpora).SelectMany(c => c.Files)
        );
        List<(string Location, ParatextProjectSettings Settings)> paratextProjects = [];
        foreach (CorpusFileContract file in corpusFiles.Where(f => f.Format == FileFormat.Paratext))
        {
            using IZipContainer archive = new ZipContainer(file.Location);
            ParatextProjectSettings settings = new ZipParatextProjectSettingsParser(archive).Parse();
            paratextProjects.Add((file.Location, settings));
        }

        foreach ((string daughterLocation, ParatextProjectSettings daughterSettings) in paratextProjects)
        {
            _settings[daughterLocation] = (daughterSettings, null, null);
            foreach ((string parentLocation, ParatextProjectSettings parentSettings) in paratextProjects)
            {
                if (
                    daughterSettings != parentSettings
                    && daughterSettings.HasParent
                    && daughterSettings.IsDaughterProjectOf(parentSettings)
                )
                {
                    daughterSettings.Parent = parentSettings;
                    _settings[daughterLocation] = (daughterSettings, parentLocation, parentSettings);
                    break;
                }
            }
        }

        SourceTextCorpora = parallelCorpora.SelectMany(parallelCorpus =>
            parallelCorpus.SourceCorpora.Select(corpus =>
                (
                    parallelCorpus,
                    corpus,
                    (IReadOnlyList<CorpusFileContract>)corpus.Files,
                    CreateTextCorpora(corpus.Files)
                )
            )
        );

        TargetTextCorpora = parallelCorpora.SelectMany(parallelCorpus =>
            parallelCorpus.TargetCorpora.Select(corpus =>
                (
                    parallelCorpus,
                    corpus,
                    (IReadOnlyList<CorpusFileContract>)corpus.Files,
                    CreateTextCorpora(corpus.Files)
                )
            )
        );

        SourceTermCorpora = parallelCorpora.SelectMany(parallelCorpus =>
            parallelCorpus.SourceCorpora.Select(corpus =>
                (
                    parallelCorpus,
                    corpus,
                    (IReadOnlyList<CorpusFileContract>)corpus.Files,
                    CreateTermCorpora(corpus.Files)
                )
            )
        );

        TargetTermCorpora = parallelCorpora.SelectMany(parallelCorpus =>
            parallelCorpus.TargetCorpora.Select(corpus =>
                (
                    parallelCorpus,
                    corpus,
                    (IReadOnlyList<CorpusFileContract>)corpus.Files,
                    CreateTermCorpora(corpus.Files)
                )
            )
        );
    }

    public (string Location, ParatextProjectSettings Settings)? ParentOf(string daughterLocation)
    {
        if (
            !_settings.TryGetValue(
                daughterLocation,
                out (ParatextProjectSettings _, string? Location, ParatextProjectSettings? Settings) parent
            )
        )
        {
            return null;
        }
        if (parent.Location == null || parent.Settings == null)
        {
            return null;
        }
        return (parent.Location, parent.Settings);
    }

    public ParatextProjectSettings? GetSettings(string location)
    {
        if (
            !_settings.TryGetValue(
                location,
                out (
                    ParatextProjectSettings ParatextProjectSettings,
                    string? ParentLocation,
                    ParatextProjectSettings? ParentSettings
                ) settings
            )
        )
        {
            return null;
        }
        return settings.ParatextProjectSettings;
    }

    public ZipParatextProjectTextUpdater GetTextUpdater(string location)
    {
        IZipContainer container = new ZipContainer(location);
        ParatextProjectSettings? parentSettings = ParentOf(location)?.Settings;
        return new ZipParatextProjectTextUpdater(container, parentSettings);
    }

    protected virtual IReadOnlyList<ITextCorpus> CreateTextCorpora(IReadOnlyList<CorpusFileContract> files)
    {
        List<ITextCorpus> corpora = [];

        List<Dictionary<string, IText>> textFileCorpora = [];
        foreach (CorpusFileContract file in files)
        {
            switch (file.Format)
            {
                case FileFormat.Text:
                    // if there are multiple texts with the same id, then add it to a new corpus or the first
                    // corpus that doesn't contain a text with that id
                    Dictionary<string, IText>? corpus = textFileCorpora.FirstOrDefault(c =>
                        !c.ContainsKey(file.TextId)
                    );
                    if (corpus is null)
                    {
                        corpus = [];
                        textFileCorpora.Add(corpus);
                    }
                    corpus[file.TextId] = new TextFileText(file.TextId, file.Location);
                    break;

                case FileFormat.Paratext:
                    string? parentLocation = null;
                    if (
                        _settings.TryGetValue(
                            file.Location,
                            out (ParatextProjectSettings, string? ParentLocation, ParatextProjectSettings?) settings
                        )
                    )
                    {
                        parentLocation = settings.ParentLocation;
                    }
                    corpora.Add(
                        new ParatextBackupTextCorpus(
                            file.Location,
                            includeAllText: true,
                            parentFileName: parentLocation
                        )
                    );
                    break;
            }
        }
        foreach (Dictionary<string, IText> corpus in textFileCorpora)
            corpora.Add(new DictionaryTextCorpus(corpus.Values));

        return corpora;
    }

    private IReadOnlyList<ITextCorpus> CreateTermCorpora(IReadOnlyList<CorpusFileContract> files)
    {
        List<ITextCorpus> corpora = [];
        foreach (CorpusFileContract file in files)
        {
            switch (file.Format)
            {
                case FileFormat.Paratext:
                    string? parentLocation = null;
                    if (
                        _settings.TryGetValue(
                            file.Location,
                            out (ParatextProjectSettings, string? ParentLocation, ParatextProjectSettings?) settings
                        )
                    )
                    {
                        parentLocation = settings.ParentLocation;
                    }
                    corpora.Add(new ParatextBackupTermsCorpus(file.Location, ["PN"], parentFileName: parentLocation));
                    break;
            }
        }
        return corpora;
    }
}
