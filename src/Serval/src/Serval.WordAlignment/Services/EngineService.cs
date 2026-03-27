namespace Serval.WordAlignment.Services;

public class EngineService(
    IRepository<Engine> engines,
    IRepository<Build> builds,
    IRepository<Models.WordAlignment> wordAlignments,
    IEngineServiceFactory engineServiceFactory,
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory,
    IOptionsMonitor<WordAlignmentOptions> wordAlignmentOptions
) : OwnedEntityServiceBase<Engine>(engines), IEngineService
{
    private readonly IRepository<Build> _builds = builds;
    private readonly IRepository<Models.WordAlignment> _wordAlignments = wordAlignments;
    private readonly IEngineServiceFactory _engineServiceFactory = engineServiceFactory;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly ILogger<EngineService> _logger = loggerFactory.CreateLogger<EngineService>();
    private readonly IOptionsMonitor<WordAlignmentOptions> _wordAlignmentOptions = wordAlignmentOptions;

    public override async Task<IEnumerable<Engine>> GetAllAsync(
        string owner,
        CancellationToken cancellationToken = default
    )
    {
        return await Entities.GetAllAsync(e => e.Owner == owner, cancellationToken);
    }

    public async Task<WordAlignmentResult?> GetWordAlignmentAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);
        if (engine.ModelRevision == 0)
            return null;

        try
        {
            return await _engineServiceFactory
                .GetEngineService(engine.Type)
                .AlignAsync(engine.Id, sourceSegment, targetSegment, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public override async Task<Engine> CreateAsync(Engine engine, CancellationToken cancellationToken = default)
    {
        if (!_wordAlignmentOptions.CurrentValue.Engines.Any(e => e.Type == engine.Type))
            throw new InvalidOperationException($"'{engine.Type}' is an invalid engine type.");

        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                engine.DateCreated = DateTime.UtcNow;
                await Entities.InsertAsync(engine, cancellationToken);

                await _engineServiceFactory
                    .GetEngineService(engine.Type)
                    .CreateAsync(engine.Id, engine.SourceLanguage, engine.TargetLanguage, engine.Name, ct);
            },
            cancellationToken
        );
        return engine;
    }

    public override async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? engine = await Entities.DeleteAsync(engineId, ct);
                if (engine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

                await _builds.DeleteAllAsync(b => b.EngineRef == engineId, ct);
                await _wordAlignments.DeleteAllAsync(wa => wa.EngineRef == engineId, ct);

                await _engineServiceFactory.GetEngineService(engine.Type).DeleteAsync(engine.Id, ct);
            },
            cancellationToken
        );
    }

    protected virtual Dictionary<string, List<int>> GetChapters(string fileLocation, string scriptureRange)
    {
        try
        {
            using var archive = new ZipContainer(
                Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, fileLocation)
            );
            return ScriptureRangeParser.GetChapters(
                scriptureRange,
                new ZipParatextProjectSettingsParser(archive).Parse().Versification
            );
        }
        catch (ArgumentException ae)
        {
            throw new InvalidOperationException($"The scripture range {scriptureRange} is not valid: {ae.Message}");
        }
    }

    public async Task<bool> StartBuildAsync(Build build, CancellationToken cancellationToken = default)
    {
        return await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                if (
                    await _builds.ExistsAsync(
                        b =>
                            b.EngineRef == build.EngineRef
                            && (b.State == JobState.Active || b.State == JobState.Pending),
                        ct
                    )
                )
                {
                    return false;
                }

                build.DateCreated = DateTime.UtcNow;
                await _builds.InsertAsync(build, cancellationToken);

                Engine engine = await GetAsync(build.EngineRef, cancellationToken);
                Dictionary<string, TrainingCorpus>? trainOn = build.TrainOn?.ToDictionary(c => c.ParallelCorpusRef);
                Dictionary<string, WordAlignmentCorpus>? wordAlignOn = build.WordAlignOn?.ToDictionary(c =>
                    c.ParallelCorpusRef
                );
                IReadOnlyList<ParallelCorpus> parallelCorpora = engine
                    .ParallelCorpora.Where(pc =>
                        trainOn == null
                        || trainOn.ContainsKey(pc.Id)
                        || wordAlignOn == null
                        || wordAlignOn.ContainsKey(pc.Id)
                    )
                    .ToList();

                IReadOnlyList<FilteredParallelCorpus> corpora = parallelCorpora
                    .Select(c =>
                        MapToFilteredCorpus(
                            c,
                            trainOn?.GetValueOrDefault(c.Id),
                            wordAlignOn?.GetValueOrDefault(c.Id),
                            trainOn is null,
                            wordAlignOn is null
                        )
                    )
                    .ToList();

                string? buildOptions = null;
                if (build.Options is not null)
                    buildOptions = JsonSerializer.Serialize(build.Options);

                // Log the build request summary
                try
                {
                    var buildRequestSummary = new JsonObject
                    {
                        ["Event"] = "BuildRequest",
                        ["EngineId"] = engine.Id,
                        ["BuildId"] = build.Id,
                        ["CorpusCount"] = corpora.Count,
                        ["ModelRevision"] = engine.ModelRevision,
                        ["ClientId"] = engine.Owner,
                    };
                    try
                    {
                        buildRequestSummary.Add("Options", JsonNode.Parse(buildOptions ?? "null"));
                    }
                    catch (JsonException)
                    {
                        buildRequestSummary.Add(
                            "Options",
                            "Build \"Options\" failed parsing: " + (buildOptions ?? "null")
                        );
                    }
                    _logger.LogInformation("{request}", buildRequestSummary.ToJsonString());
                }
                catch (JsonException)
                {
                    _logger.LogInformation("Error parsing build request summary.");
                }

                await _engineServiceFactory
                    .GetEngineService(engine.Type)
                    .StartBuildAsync(engine.Id, build.Id, corpora, buildOptions, ct);
                return true;
            },
            cancellationToken
        );
    }

    public async Task<Build?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        Engine engine = await GetAsync(engineId, cancellationToken);
        Build? currentBuild = await _builds.GetAsync(
            b => b.EngineRef == engine.Id && (b.State == JobState.Active || b.State == JobState.Pending),
            cancellationToken
        );

        await _engineServiceFactory.GetEngineService(engine.Type).CancelBuildAsync(engine.Id, cancellationToken);

        return currentBuild;
    }

    public Task AddParallelCorpusAsync(
        string engineId,
        ParallelCorpus corpus,
        CancellationToken cancellationToken = default
    )
    {
        return Entities.UpdateAsync(
            e => e.Id == engineId,
            u => u.Add(e => e.ParallelCorpora, corpus),
            cancellationToken: cancellationToken
        );
    }

    public async Task<ParallelCorpus> UpdateParallelCorpusAsync(
        string engineId,
        string parallelCorpusId,
        IReadOnlyList<MonolingualCorpus>? sourceCorpora,
        IReadOnlyList<MonolingualCorpus>? targetCorpora,
        CancellationToken cancellationToken = default
    )
    {
        ParallelCorpus? parallelCorpus = null;
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? engine = await Entities.UpdateAsync(
                    e => e.Id == engineId && e.ParallelCorpora.Any(c => c.Id == parallelCorpusId),
                    u =>
                    {
                        if (sourceCorpora is not null)
                            u.Set(c => c.ParallelCorpora.FirstMatchingElement().SourceCorpora, sourceCorpora);
                        if (targetCorpora is not null)
                            u.Set(c => c.ParallelCorpora.FirstMatchingElement().TargetCorpora, targetCorpora);
                    },
                    cancellationToken: cancellationToken
                );
                if (engine is null)
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{parallelCorpusId}' in Engine '{engineId}'."
                    );
                }
                await _wordAlignments.DeleteAllAsync(
                    wa => wa.CorpusRef == parallelCorpusId,
                    cancellationToken: cancellationToken
                );
                parallelCorpus = engine.ParallelCorpora.First(c => c.Id == parallelCorpusId);
            },
            cancellationToken: cancellationToken
        );
        if (parallelCorpus is null)
        {
            throw new EntityNotFoundException(
                $"Could not find the Corpus '{parallelCorpusId}' in Engine '{engineId}'."
            );
        }
        return parallelCorpus;
    }

    public async Task DeleteParallelCorpusAsync(
        string engineId,
        string parallelCorpusId,
        CancellationToken cancellationToken = default
    )
    {
        Engine? originalEngine = null;
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                originalEngine = await Entities.UpdateAsync(
                    e => e.Id == engineId,
                    u => u.RemoveAll(e => e.ParallelCorpora, c => c.Id == parallelCorpusId),
                    returnOriginal: true,
                    cancellationToken: ct
                );
                if (originalEngine is null || !originalEngine.ParallelCorpora.Any(c => c.Id == parallelCorpusId))
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{parallelCorpusId}' in Engine '{engineId}'."
                    );
                }
                await _wordAlignments.DeleteAllAsync(wa => wa.CorpusRef == parallelCorpusId, ct);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task DeleteAllCorpusFilesAsync(string dataFileId, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                HashSet<string> parallelCorpusIds = (
                    await Entities.GetAllAsync(
                        e =>
                            e.ParallelCorpora.Any(c =>
                                c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == dataFileId))
                                || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                            ),
                        cancellationToken: cancellationToken
                    )
                )
                    .SelectMany(e => e.ParallelCorpora.Select(c => c.Id))
                    .ToHashSet();

                await Entities.UpdateAllAsync(
                    e =>
                        e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(sc => sc.Files.Any(f => f.Id == dataFileId))
                            || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                        ),
                    u =>
                    {
                        u.RemoveAll(
                            e => e.ParallelCorpora.AllElements().SourceCorpora.AllElements().Files,
                            f => f.Id == dataFileId
                        );
                        u.RemoveAll(
                            e => e.ParallelCorpora.AllElements().TargetCorpora.AllElements().Files,
                            f => f.Id == dataFileId
                        );
                    },
                    cancellationToken: cancellationToken
                );

                await _wordAlignments.DeleteAllAsync(
                    wa => parallelCorpusIds.Contains(wa.CorpusRef),
                    cancellationToken: cancellationToken
                );
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateDataFileFilenameFilesAsync(
        string dataFileId,
        string filename,
        CancellationToken cancellationToken = default
    )
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await Entities.UpdateAllAsync(
                    e =>
                        e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == dataFileId))
                            || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                        ),
                    u =>
                    {
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().SourceCorpora.AllElements().Files,
                            f => f.Filename,
                            filename,
                            f => f.Id == dataFileId
                        );
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().TargetCorpora.AllElements().Files,
                            f => f.Filename,
                            filename,
                            f => f.Id == dataFileId
                        );
                    },
                    cancellationToken: cancellationToken
                );

                HashSet<string> parallelCorpusIds = (
                    await Entities.GetAllAsync(
                        e =>
                            e.ParallelCorpora.Any(c =>
                                c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == dataFileId))
                                || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                            ),
                        cancellationToken: cancellationToken
                    )
                )
                    .SelectMany(e => e.ParallelCorpora.Select(c => c.Id))
                    .ToHashSet();

                await _wordAlignments.DeleteAllAsync(
                    wa => parallelCorpusIds.Contains(wa.CorpusRef),
                    cancellationToken: cancellationToken
                );
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task UpdateCorpusFilesAsync(
        string corpusId,
        IReadOnlyList<CorpusFile> files,
        CancellationToken cancellationToken = default
    )
    {
        await Entities.UpdateAllAsync(
            e =>
                e.ParallelCorpora.Any(c =>
                    c.SourceCorpora.Any(sc => sc.Id == corpusId) || c.TargetCorpora.Any(tc => tc.Id == corpusId)
                ),
            u =>
            {
                u.SetAll(
                    e => e.ParallelCorpora.AllElements().SourceCorpora,
                    mc => mc.Files,
                    files,
                    mc => mc.Id == corpusId
                );
                u.SetAll(
                    e => e.ParallelCorpora.AllElements().TargetCorpora,
                    mc => mc.Files,
                    files,
                    mc => mc.Id == corpusId
                );
            },
            cancellationToken: cancellationToken
        );

        await _wordAlignments.DeleteAllAsync(wa => wa.CorpusRef == corpusId, cancellationToken: cancellationToken);
    }

    public async Task<Queue> GetQueueAsync(string engineType, CancellationToken cancellationToken = default)
    {
        int size = await _engineServiceFactory.GetEngineService(engineType).GetQueueSizeAsync(cancellationToken);
        return new Queue { Size = size, EngineType = engineType };
    }

    private FilteredParallelCorpus MapToFilteredCorpus(
        ParallelCorpus source,
        TrainingCorpus? trainingCorpus,
        WordAlignmentCorpus? wordAlignmentCorpus,
        bool trainOnAllCorpora,
        bool wordAlignOnAllCorpora
    )
    {
        string? referenceFileLocation =
            source.TargetCorpora.Count > 0 && source.TargetCorpora[0].Files.Count > 0
                ? Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, source.TargetCorpora[0].Files[0].Filename)
                : null;

        bool trainOnAllSources =
            trainOnAllCorpora || (trainingCorpus is not null && trainingCorpus.SourceFilters is null);
        bool wordAlignAllSources =
            wordAlignOnAllCorpora || (wordAlignmentCorpus is not null && wordAlignmentCorpus.SourceFilters is null);

        bool trainOnAllTargets =
            trainOnAllCorpora || (trainingCorpus is not null && trainingCorpus.TargetFilters is null);
        bool wordAlignAllTargets =
            wordAlignOnAllCorpora || (wordAlignmentCorpus is not null && wordAlignmentCorpus.TargetFilters is null);

        return new FilteredParallelCorpus
        {
            Id = source.Id,
            SourceCorpora = source
                .SourceCorpora.Select(sc =>
                    MapToFilteredMonolingualCorpus(
                        sc,
                        trainingCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                        wordAlignmentCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                        referenceFileLocation,
                        trainOnAllSources,
                        wordAlignAllSources
                    )
                )
                .ToList(),
            TargetCorpora = source
                .TargetCorpora.Select(tc =>
                    MapToFilteredMonolingualCorpus(
                        tc,
                        trainingCorpus?.TargetFilters?.Where(sf => sf.CorpusRef == tc.Id).FirstOrDefault(),
                        null,
                        referenceFileLocation,
                        trainOnAllTargets,
                        wordAlignAllTargets
                    )
                )
                .ToList(),
        };
    }

    private FilteredMonolingualCorpus MapToFilteredMonolingualCorpus(
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
            trainOnChapters = GetChapters(referenceFileLocation, trainingFilter.ScriptureRange)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToHashSet());
        }

        Dictionary<string, HashSet<int>>? wordAlignmentChapters = null;
        if (
            wordAlignmentFilter is not null
            && wordAlignmentFilter.ScriptureRange is not null
            && referenceFileLocation is not null
        )
        {
            wordAlignmentChapters = GetChapters(referenceFileLocation, wordAlignmentFilter.ScriptureRange)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToHashSet());
        }

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

        if (
            wordAlignmentFilter is not null
            && wordAlignmentFilter.TextIds is not null
            && wordAlignmentFilter.ScriptureRange is not null
        )
        {
            throw new InvalidOperationException(
                "Cannot specify both TextIds and ScriptureRange in the word alignment filter."
            );
        }

        var result = new FilteredMonolingualCorpus
        {
            Id = inputCorpus.Id,
            Language = inputCorpus.Language,
            Files = inputCorpus
                .Files.Select(f => new ResolvedCorpusFile
                {
                    TextId = f.TextId,
                    Format = f.Format,
                    Location = Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, f.Filename),
                })
                .ToList(),
        };

        if (
            trainOnAll
            || (trainingFilter is not null && trainingFilter.TextIds is null && trainingFilter.ScriptureRange is null)
        )
        {
            result.TrainOnAll = true;
        }
        else
        {
            if (trainOnChapters is not null)
                result.TrainOnChapters = trainOnChapters;
            if (trainingFilter?.TextIds is not null)
                result.TrainOnTextIds = trainingFilter.TextIds.ToHashSet();
        }

        if (
            wordAlignOnAll
            || (
                wordAlignmentFilter is not null
                && wordAlignmentFilter.TextIds is null
                && wordAlignmentFilter.ScriptureRange is null
            )
        )
        {
            result.PretranslateAll = true;
        }
        else
        {
            if (wordAlignmentChapters is not null)
                result.InferenceChapters = wordAlignmentChapters;
            if (wordAlignmentFilter?.TextIds is not null)
                result.InferenceTextIds = wordAlignmentFilter.TextIds.ToHashSet();
        }

        return result;
    }
}
