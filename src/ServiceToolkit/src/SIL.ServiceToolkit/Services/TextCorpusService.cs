namespace SIL.ServiceToolkit.Services;

public class TextCorpusService : ITextCorpusService
{
    public IEnumerable<ITextCorpus> CreateTextCorpora(
        IReadOnlyList<CorpusFile> files,
        IReadOnlyList<CorpusFile> referenceFiles
    )
    {
        IReadOnlyDictionary<string, string> parentLocations = GetParentProjectLocations(
            referenceFiles.Where(rf => rf.Format == FileFormat.Paratext).Select(rf => rf.Location).ToArray()
        );

        List<ITextCorpus> corpora = [];

        List<Dictionary<string, IText>> textFileCorpora = [];
        foreach (CorpusFile file in files)
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
                    if (!parentLocations.TryGetValue(file.Location, out string? parentFileName))
                        parentFileName = null;
                    corpora.Add(
                        new ParatextBackupTextCorpus(
                            file.Location,
                            includeAllText: true,
                            parentFileName: parentFileName
                        )
                    );
                    break;
            }
        }
        foreach (Dictionary<string, IText> corpus in textFileCorpora)
            corpora.Add(new DictionaryTextCorpus(corpus.Values));

        return corpora;
    }

    public IReadOnlyDictionary<string, string> GetParentProjectLocations(IReadOnlyList<string> referenceFiles)
    {
        List<(string Location, ParatextProjectSettings Settings)> referenceSettings = [];
        foreach (string referenceFile in referenceFiles)
        {
            using ZipArchive archive = ZipFile.OpenRead(referenceFile);
            ParatextProjectSettings settings = ZipParatextProjectSettingsParser.Parse(archive);
            referenceSettings.Add((referenceFile, settings));
        }

        Dictionary<string, string> parentLocations = [];
        foreach ((string daughterLocation, ParatextProjectSettings daughterSettings) in referenceSettings)
        {
            foreach ((string parentLocation, ParatextProjectSettings parentSettings) in referenceSettings)
            {
                if (
                    daughterSettings == parentSettings
                    || !daughterSettings.HasParent
                    || !daughterSettings.IsDaughterProjectOf(parentSettings)
                )
                {
                    continue;
                }

                parentLocations[daughterLocation] = parentLocation;
            }
        }

        return parentLocations;
    }

    public IEnumerable<ITextCorpus> CreateTermCorpora(IReadOnlyList<CorpusFile> files)
    {
        foreach (CorpusFile file in files)
        {
            switch (file.Format)
            {
                case FileFormat.Paratext:
                    yield return new ParatextBackupTermsCorpus(file.Location, ["PN"]);
                    break;
            }
        }
    }
}
