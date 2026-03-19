namespace Serval.Translation.Services;

public class CorpusMappingService(
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    IParallelCorpusService parallelCorpusService
) : ICorpusMappingService
{
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;
    private readonly IParallelCorpusService _parallelCorpusService = parallelCorpusService;

    public IReadOnlyList<SIL.ServiceToolkit.Models.ParallelCorpus> Map(Build build, Engine engine)
    {
        if (engine.ParallelCorpora.Any())
        {
            return Map(build, engine.ParallelCorpora);
        }
        else
        {
            return Map(build, engine, engine.Corpora);
        }
    }

    public IReadOnlyList<SIL.ServiceToolkit.Models.ParallelCorpus> Map(
        Build build,
        Engine engine,
        IReadOnlyList<Corpus> corpora
    )
    {
        List<SIL.ServiceToolkit.Models.ParallelCorpus> mappedParallelCorpora = [];

        Dictionary<string, TrainingCorpus>? trainingCorpora = build.TrainOn?.ToDictionary(c => c.CorpusRef!);
        Dictionary<string, PretranslateCorpus>? pretranslateCorpora = build.Pretranslate?.ToDictionary(c =>
            c.CorpusRef!
        );
        bool trainOnAllCorpora = trainingCorpora is null;
        bool pretranslateAllCorpora = pretranslateCorpora is null;

        foreach (
            Corpus source in corpora.Where(c =>
                trainingCorpora == null
                || trainingCorpora.ContainsKey(c.Id)
                || pretranslateCorpora == null
                || pretranslateCorpora.ContainsKey(c.Id)
            )
        )
        {
            TrainingCorpus? trainingCorpus = trainingCorpora?.GetValueOrDefault(source.Id);
            PretranslateCorpus? pretranslateCorpus = pretranslateCorpora?.GetValueOrDefault(source.Id);

            IEnumerable<SIL.ServiceToolkit.Models.CorpusFile> sourceFiles = source.SourceFiles.Select(Map);
            IEnumerable<SIL.ServiceToolkit.Models.CorpusFile> targetFiles = source.TargetFiles.Select(Map);
            SIL.ServiceToolkit.Models.MonolingualCorpus sourceCorpus = new()
            {
                Id = source.Id,
                Language = source.SourceLanguage,
                Files = source.SourceFiles.Select(Map).ToArray(),
                TrainOnAll = trainOnAllCorpora,
                PretranslateAll = pretranslateAllCorpora,
            };
            SIL.ServiceToolkit.Models.MonolingualCorpus targetCorpus = new()
            {
                Id = source.Id,
                Language = source.TargetLanguage,
                Files = source.TargetFiles.Select(Map).ToArray(),
                TrainOnAll = trainOnAllCorpora,
                PretranslateAll = pretranslateAllCorpora,
            };

            if (trainingCorpus is not null)
            {
                if (trainingCorpus.TextIds is not null && trainingCorpus.ScriptureRange is not null)
                {
                    throw new InvalidOperationException(
                        $"The corpus {source.Id} cannot specify both 'textIds' and 'scriptureRange' for trainOn"
                    );
                }
                sourceCorpus.TrainOnTextIds = trainingCorpus.TextIds?.ToHashSet();
                targetCorpus.TrainOnTextIds = trainingCorpus.TextIds?.ToHashSet();

                if (trainingCorpus.ScriptureRange is not null)
                {
                    if (
                        targetCorpus.Files.Count > 1
                        || targetCorpus.Files[0].Format != SIL.ServiceToolkit.Models.FileFormat.Paratext
                    )
                    {
                        throw new InvalidOperationException(
                            $"The corpus {source.Id} is not compatible with using a scripture range"
                        );
                    }
                    var chapters = _parallelCorpusService
                        .GetChapters(
                            corpora.Select(c => Map(c, engine)).ToArray(),
                            GetFilePath(targetCorpus.Files[0].Location),
                            trainingCorpus.ScriptureRange
                        )
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToHashSet());
                    sourceCorpus.TrainOnChapters = chapters;
                    targetCorpus.TrainOnChapters = chapters;
                }
                sourceCorpus.TrainOnAll = sourceCorpus.TrainOnChapters is null && sourceCorpus.TrainOnTextIds is null;
                targetCorpus.TrainOnAll = targetCorpus.TrainOnChapters is null && targetCorpus.TrainOnTextIds is null;
            }

            if (pretranslateCorpus is not null)
            {
                if (pretranslateCorpus.TextIds is not null && pretranslateCorpus.ScriptureRange is not null)
                {
                    throw new InvalidOperationException(
                        $"The corpus {source.Id} cannot specify both 'textIds' and 'scriptureRange' for 'pretranslate'."
                    );
                }
                sourceCorpus.InferenceTextIds = pretranslateCorpus.TextIds?.ToHashSet();
                if (pretranslateCorpus.ScriptureRange is not null)
                {
                    if (
                        targetCorpus.Files.Count > 1
                        || targetCorpus.Files[0].Format != SIL.ServiceToolkit.Models.FileFormat.Paratext
                    )
                    {
                        throw new InvalidOperationException(
                            $"The corpus {source.Id} is not compatible with using a scripture range"
                        );
                    }
                    sourceCorpus.InferenceChapters = _parallelCorpusService
                        .GetChapters(
                            corpora.Select(c => Map(c, engine)).ToArray(),
                            GetFilePath(targetCorpus.Files[0].Location),
                            pretranslateCorpus.ScriptureRange
                        )
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToHashSet());
                }
                sourceCorpus.PretranslateAll =
                    sourceCorpus.InferenceChapters is null && sourceCorpus.InferenceTextIds is null;
                targetCorpus.PretranslateAll =
                    targetCorpus.InferenceChapters is null && targetCorpus.InferenceTextIds is null;
            }
            SIL.ServiceToolkit.Models.ParallelCorpus corpus = new()
            {
                Id = source.Id,
                SourceCorpora = [sourceCorpus],
                TargetCorpora = [targetCorpus],
            };
            mappedParallelCorpora.Add(corpus);
        }
        return mappedParallelCorpora;
    }

    private IReadOnlyList<SIL.ServiceToolkit.Models.ParallelCorpus> Map(
        Build build,
        IReadOnlyList<ParallelCorpus> parallelCorpora
    )
    {
        List<SIL.ServiceToolkit.Models.ParallelCorpus> mappedParallelCorpora = [];
        Dictionary<string, TrainingCorpus>? trainingCorpora = build.TrainOn?.ToDictionary(c => c.ParallelCorpusRef!);
        Dictionary<string, PretranslateCorpus>? pretranslateCorpora = build.Pretranslate?.ToDictionary(c =>
            c.ParallelCorpusRef!
        );

        bool trainOnAllCorpora = trainingCorpora is null;
        bool pretranslateAllCorpora = pretranslateCorpora is null;

        parallelCorpora = parallelCorpora
            .Where(pc =>
                trainingCorpora == null
                || trainingCorpora.ContainsKey(pc.Id)
                || pretranslateCorpora == null
                || pretranslateCorpora.ContainsKey(pc.Id)
            )
            .ToArray();
        foreach (ParallelCorpus source in parallelCorpora)
        {
            TrainingCorpus? trainingCorpus = trainingCorpora?.GetValueOrDefault(source.Id);
            PretranslateCorpus? pretranslateCorpus = pretranslateCorpora?.GetValueOrDefault(source.Id);

            string? referenceFileLocation =
                source.TargetCorpora.Count > 0 && source.TargetCorpora[0].Files.Count > 0
                    ? Map(source.TargetCorpora[0].Files[0]).Location
                    : null;

            mappedParallelCorpora.Add(
                new SIL.ServiceToolkit.Models.ParallelCorpus
                {
                    Id = source.Id,
                    SourceCorpora = source
                        .SourceCorpora.Select(sc =>
                            Map(
                                parallelCorpora,
                                sc,
                                trainingCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                                pretranslateCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                                referenceFileLocation,
                                trainOnAllCorpora
                                    || (trainingCorpus is not null && trainingCorpus.SourceFilters is null),
                                pretranslateAllCorpora
                                    || (pretranslateCorpus is not null && pretranslateCorpus.SourceFilters is null)
                            )
                        )
                        .ToArray(),
                    TargetCorpora = source
                        .TargetCorpora.Select(tc =>
                            Map(
                                parallelCorpora,
                                tc,
                                trainingCorpus?.TargetFilters?.Where(sf => sf.CorpusRef == tc.Id).FirstOrDefault(),
                                null,
                                referenceFileLocation,
                                trainOnAllCorpora
                                    || (trainingCorpus is not null && trainingCorpus.TargetFilters is null),
                                pretranslateAllCorpora || pretranslateCorpus is not null
                            )
                        )
                        .ToArray(),
                }
            );
        }
        return mappedParallelCorpora;
    }

    private SIL.ServiceToolkit.Models.MonolingualCorpus Map(
        IReadOnlyList<ParallelCorpus> parallelCorpora,
        MonolingualCorpus inputCorpus,
        ParallelCorpusFilter? trainingFilter,
        ParallelCorpusFilter? pretranslateFilter,
        string? referenceFileLocation,
        bool trainOnAll,
        bool pretranslateAll
    )
    {
        Dictionary<string, HashSet<int>>? trainOnChapters = null;
        if (
            trainingFilter is not null
            && trainingFilter.ScriptureRange is not null
            && referenceFileLocation is not null
        )
        {
            trainOnChapters = _parallelCorpusService
                .GetChapters(
                    parallelCorpora.Select(Map).ToArray(),
                    GetFilePath(referenceFileLocation),
                    trainingFilter.ScriptureRange
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToHashSet());
        }

        Dictionary<string, HashSet<int>>? pretranslateChapters = null;
        if (
            pretranslateFilter is not null
            && pretranslateFilter.ScriptureRange is not null
            && referenceFileLocation is not null
        )
        {
            pretranslateChapters = _parallelCorpusService
                .GetChapters(
                    parallelCorpora.Select(Map).ToArray(),
                    GetFilePath(referenceFileLocation),
                    pretranslateFilter.ScriptureRange
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToHashSet());
        }

        var returnCorpus = new SIL.ServiceToolkit.Models.MonolingualCorpus
        {
            Id = inputCorpus.Id,
            Language = inputCorpus.Language,
            Files = inputCorpus.Files.Select(Map).ToArray(),
            TrainOnAll = trainOnAll,
            PretranslateAll = pretranslateAll,
        };

        if (
            trainingFilter is not null
            && trainingFilter.TextIds is not null
            && trainingFilter.ScriptureRange is not null
        )
        {
            throw new InvalidOperationException(
                "Cannot specify both TextIds and ScriptureRange in the training filter."
            );
        }

        returnCorpus.TrainOnChapters = trainOnChapters;
        returnCorpus.TrainOnTextIds = trainingFilter?.TextIds?.ToHashSet();

        if (
            pretranslateFilter is not null
            && pretranslateFilter.TextIds is not null
            && pretranslateFilter.ScriptureRange is not null
        )
        {
            throw new InvalidOperationException(
                "Cannot specify both TextIds and ScriptureRange in the pretranslation filter."
            );
        }

        returnCorpus.InferenceChapters = pretranslateChapters;
        returnCorpus.InferenceTextIds = pretranslateFilter?.TextIds?.ToHashSet();

        return returnCorpus;
    }

    public SIL.ServiceToolkit.Models.ParallelCorpus Map(Corpus source, Engine engine)
    {
        return new SIL.ServiceToolkit.Models.ParallelCorpus
        {
            Id = source.Id,
            SourceCorpora = source.SourceFiles.Select(f => Map(f, engine.SourceLanguage)).ToArray(),
            TargetCorpora = source.TargetFiles.Select(f => Map(f, engine.TargetLanguage)).ToArray(),
        };
    }

    private SIL.ServiceToolkit.Models.MonolingualCorpus Map(CorpusFile source, string language)
    {
        return new SIL.ServiceToolkit.Models.MonolingualCorpus
        {
            Id = source.Id,
            Language = language,
            Files = [Map(source)],
        };
    }

    private SIL.ServiceToolkit.Models.CorpusFile Map(CorpusFile source)
    {
        return new SIL.ServiceToolkit.Models.CorpusFile
        {
            Location = GetFilePath(source.Filename),
            Format = (SIL.ServiceToolkit.Models.FileFormat)source.Format,
            TextId = source.TextId,
        };
    }

    private SIL.ServiceToolkit.Models.ParallelCorpus Map(ParallelCorpus source)
    {
        return new SIL.ServiceToolkit.Models.ParallelCorpus
        {
            Id = source.Id,
            SourceCorpora = source.SourceCorpora.Select(Map).ToArray(),
            TargetCorpora = source.TargetCorpora.Select(Map).ToArray(),
        };
    }

    private SIL.ServiceToolkit.Models.MonolingualCorpus Map(MonolingualCorpus source)
    {
        return new SIL.ServiceToolkit.Models.MonolingualCorpus
        {
            Id = source.Id,
            Language = source.Language,
            Files = source.Files.Select(Map).ToList(),
        };
    }

    private string GetFilePath(string filename)
    {
        return Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, filename);
    }
}
