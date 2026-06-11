namespace Serval.WordAlignment.Services;

public class ContractMapper(
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    IParallelCorpusService parallelCorpusService
)
{
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;
    private readonly IParallelCorpusService _parallelCorpusService = parallelCorpusService;

    public IReadOnlyList<ParallelCorpusContract> Map(Build build, Engine engine)
    {
        return Map(build, engine.ParallelCorpora);
    }

    private IReadOnlyList<ParallelCorpusContract> Map(Build build, IReadOnlyList<ParallelCorpus> parallelCorpora)
    {
        List<ParallelCorpusContract> mappedParallelCorpora = [];
        Dictionary<string, TrainingCorpus>? trainingCorpora = build.TrainOn?.ToDictionary(c => c.ParallelCorpusRef!);
        Dictionary<string, WordAlignmentCorpus>? wordAlignOnCorpora = build.WordAlignOn?.ToDictionary(c =>
            c.ParallelCorpusRef!
        );

        bool trainOnAllCorpora = trainingCorpora is null;
        bool wordAlignOnAllCorpora = wordAlignOnCorpora is null;

        parallelCorpora = parallelCorpora
            .Where(pc =>
                trainingCorpora == null
                || trainingCorpora.ContainsKey(pc.Id)
                || wordAlignOnCorpora == null
                || wordAlignOnCorpora.ContainsKey(pc.Id)
            )
            .ToArray();
        foreach (ParallelCorpus source in parallelCorpora)
        {
            TrainingCorpus? trainingCorpus = trainingCorpora?.GetValueOrDefault(source.Id);
            WordAlignmentCorpus? wordAlignOnCorpus = wordAlignOnCorpora?.GetValueOrDefault(source.Id);

            string? referenceFileLocation =
                source.TargetCorpora.Count > 0 && source.TargetCorpora[0].Files.Count > 0
                    ? Map(source.TargetCorpora[0].Files[0]).Location
                    : null;

            mappedParallelCorpora.Add(
                new ParallelCorpusContract
                {
                    Id = source.Id,
                    SourceCorpora =
                    [
                        .. source.SourceCorpora.Select(sc =>
                            Map(
                                parallelCorpora,
                                sc,
                                trainingCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                                wordAlignOnCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                                referenceFileLocation,
                                trainOnAllCorpora
                                    || (trainingCorpus is not null && trainingCorpus.SourceFilters is null),
                                wordAlignOnAllCorpora
                                    || (wordAlignOnCorpus is not null && wordAlignOnCorpus.SourceFilters is null)
                            )
                        ),
                    ],
                    TargetCorpora =
                    [
                        .. source.TargetCorpora.Select(tc =>
                            Map(
                                parallelCorpora,
                                tc,
                                trainingCorpus?.TargetFilters?.Where(sf => sf.CorpusRef == tc.Id).FirstOrDefault(),
                                null,
                                referenceFileLocation,
                                trainOnAllCorpora
                                    || (trainingCorpus is not null && trainingCorpus.TargetFilters is null),
                                wordAlignOnAllCorpora || wordAlignOnCorpus is not null
                            )
                        ),
                    ],
                }
            );
        }
        return mappedParallelCorpora;
    }

    private MonolingualCorpusContract Map(
        IReadOnlyList<ParallelCorpus> parallelCorpora,
        MonolingualCorpus inputCorpus,
        ParallelCorpusFilter? trainingFilter,
        ParallelCorpusFilter? wordAlignmentFilter,
        string? referenceFileLocation,
        bool trainOnAll,
        bool wordAlignOnAll
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
                    [.. parallelCorpora.Select(Map)],
                    GetFilePath(referenceFileLocation),
                    trainingFilter.ScriptureRange
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToHashSet());
        }

        Dictionary<string, HashSet<int>>? wordAlignOnChapters = null;
        if (
            wordAlignmentFilter is not null
            && wordAlignmentFilter.ScriptureRange is not null
            && referenceFileLocation is not null
        )
        {
            wordAlignOnChapters = _parallelCorpusService
                .GetChapters(
                    [.. parallelCorpora.Select(Map)],
                    GetFilePath(referenceFileLocation),
                    wordAlignmentFilter.ScriptureRange
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToHashSet());
        }

        var returnCorpus = new MonolingualCorpusContract
        {
            Id = inputCorpus.Id,
            Language = inputCorpus.Language,
            Files = [.. inputCorpus.Files.Select(Map)],
            TrainOnTextIds = trainOnAll ? null : [],
            InferenceTextIds = wordAlignOnAll ? null : [],
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
        if (trainingFilter is not null)
            returnCorpus.TrainOnTextIds = trainingFilter.TextIds?.ToHashSet();

        if (
            wordAlignmentFilter is not null
            && wordAlignmentFilter.TextIds is not null
            && wordAlignmentFilter.ScriptureRange is not null
        )
        {
            throw new InvalidOperationException(
                "Cannot specify both TextIds and ScriptureRange in the pretranslation filter."
            );
        }

        returnCorpus.InferenceChapters = wordAlignOnChapters;
        if (wordAlignmentFilter is not null)
            returnCorpus.InferenceTextIds = wordAlignmentFilter.TextIds?.ToHashSet();

        return returnCorpus;
    }

    private CorpusFileContract Map(CorpusFile source)
    {
        return new CorpusFileContract
        {
            Location = GetFilePath(source.Filename),
            Format = source.Format,
            TextId = source.TextId,
        };
    }

    private ParallelCorpusContract Map(ParallelCorpus source)
    {
        return new ParallelCorpusContract
        {
            Id = source.Id,
            SourceCorpora = [.. source.SourceCorpora.Select(Map)],
            TargetCorpora = [.. source.TargetCorpora.Select(Map)],
        };
    }

    private MonolingualCorpusContract Map(MonolingualCorpus source)
    {
        return new MonolingualCorpusContract
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
