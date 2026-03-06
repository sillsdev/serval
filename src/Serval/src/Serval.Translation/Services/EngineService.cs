using MassTransit.Mediator;

namespace Serval.Translation.Services;

public class EngineService(
    IRepository<Engine> engines,
    IRepository<Build> builds,
    IRepository<Pretranslation> pretranslations,
    IScopedMediator mediator,
    IEnumerable<ITranslationEngineService> engineServices,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory,
    IOptionsMonitor<TranslationOptions> translationOptions,
    ICorpusMappingService corpusMappingService
) : OwnedEntityServiceBase<Engine>(engines), IEngineService
{
    private readonly IRepository<Build> _builds = builds;
    private readonly IRepository<Pretranslation> _pretranslations = pretranslations;
    private readonly IScopedMediator _mediator = mediator;
    private readonly Dictionary<string, ITranslationEngineService> _engineServices = engineServices.ToDictionary(e =>
        e.Type
    );
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly ILogger<EngineService> _logger = loggerFactory.CreateLogger<EngineService>();
    private readonly IOptionsMonitor<TranslationOptions> _translationOptions = translationOptions;
    private readonly ICorpusMappingService _corpusMappingService = corpusMappingService;

    private ITranslationEngineService GetEngine(string engineType) =>
        _engineServices.TryGetValue(engineType, out ITranslationEngineService? engine)
            ? engine
            : throw new InvalidOperationException($"No engine registered for type '{engineType}'.");

    public async Task<TranslationResult?> TranslateAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);
        if (engine.ModelRevision == 0)
            return null;

        IReadOnlyList<TranslationResult> results = await GetEngine(engine.Type)
            .TranslateAsync(engine.Id, 1, segment, cancellationToken);
        return results[0];
    }

    public async Task<IEnumerable<TranslationResult>?> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);
        if (engine.ModelRevision == 0)
            return null;

        return await GetEngine(engine.Type).TranslateAsync(engine.Id, n, segment, cancellationToken);
    }

    public async Task<WordGraph?> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);
        if (engine.ModelRevision == 0)
            return null;

        return await GetEngine(engine.Type).GetWordGraphAsync(engine.Id, segment, cancellationToken);
    }

    public async Task<bool> TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);
        if (engine.ModelRevision == 0)
            return false;

        await GetEngine(engine.Type)
            .TrainSegmentPairAsync(engine.Id, sourceSegment, targetSegment, sentenceStart, cancellationToken);
        return true;
    }

    public override async Task<Engine> CreateAsync(Engine engine, CancellationToken cancellationToken = default)
    {
        if (!_translationOptions.CurrentValue.Engines.Any(e => e.Type == engine.Type))
            throw new InvalidOperationException($"'{engine.Type}' is an invalid engine type.");

        engine.DateCreated = DateTime.UtcNow;
        await Entities.InsertAsync(engine, cancellationToken);

        await GetEngine(engine.Type)
            .CreateAsync(
                engine.Id,
                engine.SourceLanguage,
                engine.TargetLanguage,
                engine.Name,
                engine.IsModelPersisted ?? false,
                cancellationToken
            );
        return engine;
    }

    public async Task UpdateAsync(
        string engineId,
        string? sourceLanguage,
        string? targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = null;
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                engine = await Entities.UpdateAsync(
                    engineId,
                    u =>
                    {
                        if (sourceLanguage is not null)
                            u.Set(e => e.SourceLanguage, sourceLanguage);
                        if (targetLanguage is not null)
                            u.Set(e => e.TargetLanguage, targetLanguage);
                    },
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");
                await _pretranslations.DeleteAllAsync(pt => pt.EngineRef == engineId, ct);
            },
            cancellationToken
        );

        await GetEngine(engine!.Type).UpdateAsync(engineId, sourceLanguage, targetLanguage, cancellationToken);
    }

    public override async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        Engine? engine = null;
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                engine = await Entities.DeleteAsync(engineId, ct);
                if (engine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

                await _builds.DeleteAllAsync(b => b.EngineRef == engineId, ct);
                await _pretranslations.DeleteAllAsync(pt => pt.EngineRef == engineId, ct);
            },
            cancellationToken
        );

        await GetEngine(engine!.Type).DeleteAsync(engineId, cancellationToken);
    }

    public async Task<bool> StartBuildAsync(Build build, CancellationToken cancellationToken = default)
    {
        Engine? engine = null;
        IReadOnlyList<FilteredParallelCorpus>? corpora = null;
        string? buildOptions = null;

        bool inserted = await _dataAccessContext.WithTransactionAsync(
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
                await _builds.InsertAsync(build, ct);

                engine = await GetAsync(build.EngineRef, ct);
                StartBuildRequest request = new StartBuildRequest
                {
                    EngineType = engine.Type,
                    EngineId = engine.Id,
                    BuildId = build.Id,
                    Corpora = { _corpusMappingService.Map(build, engine).Select(Map) },
                };

        if (!inserted || engine is null || corpora is null)
            return false;

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
                buildRequestSummary.Add("Options", "Build \"Options\" failed parsing: " + (buildOptions ?? "null"));
            }
            _logger.LogInformation("{request}", buildRequestSummary.ToJsonString());
        }
        catch (JsonException)
        {
            _logger.LogInformation("Error parsing build request summary.");
        }

        await GetEngine(engine.Type).StartBuildAsync(engine.Id, build.Id, corpora, buildOptions, cancellationToken);
        return true;
    }

    public async Task<Build?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        Engine engine = await GetAsync(engineId, cancellationToken);
        Build? currentBuild = await _builds.GetAsync(
            b => b.EngineRef == engine.Id && (b.State == JobState.Active || b.State == JobState.Pending),
            cancellationToken
        );
        if (currentBuild is null)
            return null;

        await GetEngine(engine.Type).CancelBuildAsync(engineId, cancellationToken);
        return currentBuild;
    }

    public async Task<ModelDownloadUrl?> GetModelDownloadUrlAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);
        if (engine.ModelRevision == 0)
            return null;

        return await GetEngine(engine.Type).GetModelDownloadUrlAsync(engine.Id, cancellationToken);
    }

    public Task AddCorpusAsync(string engineId, Corpus corpus, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAsync(
            e => e.Id == engineId,
            u => u.Add(e => e.Corpora, corpus),
            cancellationToken: cancellationToken
        );
    }

    public async Task<Corpus> UpdateCorpusAsync(
        string engineId,
        string corpusId,
        IReadOnlyList<CorpusFile>? sourceFiles,
        IReadOnlyList<CorpusFile>? targetFiles,
        CancellationToken cancellationToken = default
    )
    {
        Corpus? corpus = null;
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? engine = await Entities.UpdateAsync(
                    e => e.Id == engineId && e.Corpora.Any(c => c.Id == corpusId),
                    u =>
                    {
                        if (sourceFiles is not null)
                            u.Set(c => c.Corpora.FirstMatchingElement().SourceFiles, sourceFiles);
                        if (targetFiles is not null)
                            u.Set(c => c.Corpora.FirstMatchingElement().TargetFiles, targetFiles);
                    },
                    cancellationToken: cancellationToken
                );
                if (engine is null)
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{corpusId}' in Engine '{engineId}'."
                    );
                }

                await _pretranslations.DeleteAllAsync(
                    pt => pt.CorpusRef == corpusId,
                    cancellationToken: cancellationToken
                );
                corpus = engine.Corpora.First(c => c.Id == corpusId);
            },
            cancellationToken: cancellationToken
        );
        if (corpus is null)
        {
            throw new EntityNotFoundException($"Could not find the Corpus '{corpusId}' in Engine '{engineId}'.");
        }
        return corpus;
    }

    public async Task DeleteCorpusAsync(
        string engineId,
        string corpusId,
        bool deleteFiles,
        CancellationToken cancellationToken = default
    )
    {
        Engine? originalEngine = null;
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                originalEngine = await Entities.UpdateAsync(
                    e => e.Id == engineId,
                    u => u.RemoveAll(e => e.Corpora, c => c.Id == corpusId),
                    returnOriginal: true,
                    cancellationToken: ct
                );
                if (originalEngine is null || !originalEngine.Corpora.Any(c => c.Id == corpusId))
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{corpusId}' in Engine '{engineId}'."
                    );
                }
                await _pretranslations.DeleteAllAsync(pt => pt.CorpusRef == corpusId, ct);
            },
            cancellationToken: cancellationToken
        );
        if (deleteFiles && originalEngine != null)
        {
            foreach (
                string id in originalEngine.Corpora.SelectMany(c =>
                    c.TargetFiles.Select(f => f.Id).Concat(c.SourceFiles.Select(f => f.Id).Distinct())
                )
            )
            {
                await _mediator.Send<DeleteDataFile>(new { DataFileId = id }, cancellationToken);
            }
        }
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

                await _pretranslations.DeleteAllAsync(
                    pt => pt.CorpusRef == parallelCorpusId,
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
                await _pretranslations.DeleteAllAsync(pt => pt.CorpusRef == parallelCorpusId, ct);
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

                HashSet<string> corpusIds = (
                    await Entities.GetAllAsync(
                        e =>
                            e.Corpora.Any(c =>
                                c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                            ),
                        cancellationToken
                    )
                )
                    .SelectMany(e => e.Corpora.Select(c => c.Id))
                    .ToHashSet();

                await Entities.UpdateAllAsync(
                    e =>
                        e.Corpora.Any(c =>
                            c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                        )
                        || e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(sc => sc.Files.Any(f => f.Id == dataFileId))
                            || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                        ),
                    u =>
                    {
                        u.RemoveAll(e => e.Corpora.AllElements().SourceFiles, f => f.Id == dataFileId);
                        u.RemoveAll(e => e.Corpora.AllElements().TargetFiles, f => f.Id == dataFileId);
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

                await _pretranslations.DeleteAllAsync(
                    pt => parallelCorpusIds.Contains(pt.CorpusRef) || corpusIds.Contains(pt.CorpusRef),
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
                        e.Corpora.Any(c =>
                            c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                        )
                        || e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(sc => sc.Files.Any(f => f.Id == dataFileId))
                            || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                        ),
                    u =>
                    {
                        u.SetAll(
                            e => e.Corpora.AllElements().SourceFiles,
                            f => f.Filename,
                            filename,
                            f => f.Id == dataFileId
                        );
                        u.SetAll(
                            e => e.Corpora.AllElements().TargetFiles,
                            f => f.Filename,
                            filename,
                            f => f.Id == dataFileId
                        );
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

                HashSet<string> corpusIds = (
                    await Entities.GetAllAsync(
                        e =>
                            e.Corpora.Any(c =>
                                c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                            ),
                        cancellationToken
                    )
                )
                    .SelectMany(e => e.Corpora.Select(c => c.Id))
                    .ToHashSet();

                await _pretranslations.DeleteAllAsync(
                    pt => parallelCorpusIds.Contains(pt.CorpusRef) || corpusIds.Contains(pt.CorpusRef),
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
        await _pretranslations.DeleteAllAsync(pt => pt.CorpusRef == corpusId, cancellationToken: cancellationToken);
    }

    public async Task<Queue> GetQueueAsync(string engineType, CancellationToken cancellationToken = default)
    {
        int size = await GetEngine(engineType).GetQueueSizeAsync(cancellationToken);
        return new Queue { Size = size, EngineType = engineType };
    }

    public async Task<LanguageInfo> GetLanguageInfoAsync(
        string engineType,
        string language,
        CancellationToken cancellationToken = default
    )
    {
        return await GetEngine(engineType).GetLanguageInfoAsync(language, cancellationToken);
    }

    private List<FilteredParallelCorpus> BuildCorpora(Engine engine, Build build)
    {
        if (engine.ParallelCorpora.Any())
        {
            Dictionary<string, TrainingCorpus>? trainOn = build.TrainOn?.ToDictionary(c => c.ParallelCorpusRef!);
            Dictionary<string, PretranslateCorpus>? pretranslate = build.Pretranslate?.ToDictionary(c =>
                c.ParallelCorpusRef!
            );
            return engine
                .ParallelCorpora.Where(pc =>
                    trainOn == null
                    || trainOn.ContainsKey(pc.Id)
                    || pretranslate == null
                    || pretranslate.ContainsKey(pc.Id)
                )
                .Select(c =>
                    Map(
                        c,
                        trainOn?.GetValueOrDefault(c.Id),
                        pretranslate?.GetValueOrDefault(c.Id),
                        trainOn is null,
                        pretranslate is null
                    )
                )
                .ToList();
        }
        else
        {
            Dictionary<string, TrainingCorpus>? trainOn = build.TrainOn?.ToDictionary(c => c.CorpusRef!);
            Dictionary<string, PretranslateCorpus>? pretranslate = build.Pretranslate?.ToDictionary(c => c.CorpusRef!);
            return engine
                .Corpora.Where(c =>
                    trainOn == null
                    || trainOn.ContainsKey(c.Id)
                    || pretranslate == null
                    || pretranslate.ContainsKey(c.Id)
                )
                .Select(c =>
                    Map(
                        c,
                        trainOn?.GetValueOrDefault(c.Id),
                        pretranslate?.GetValueOrDefault(c.Id),
                        trainOn is null,
                        pretranslate is null
                    )
                )
                .ToList();
        }
    }

    private static V1.ParallelCorpus Map(SIL.ServiceToolkit.Models.ParallelCorpus source)
        FilteredMonolingualCorpus sourceCorpus = new()
    {
        return new V1.ParallelCorpus
            Files = { source.SourceFiles.Select(Map) },
        {
            Id = source.Id,
            SourceCorpora = { source.SourceCorpora.Select(Map) },
            TargetCorpora = { source.TargetCorpora.Select(Map) },
        };
        bool trainOnAll =
    }

    private static V1.MonolingualCorpus Map(SIL.ServiceToolkit.Models.MonolingualCorpus source)
    {
        var corpus = new V1.MonolingualCorpus
        {
            Id = source.Id,
            Language = source.Language,
            Files = { source.Files.Select(Map) },
        };

        if (source.TrainOnAll)
        // Inference filter
        bool pretranslateAll =
        if (!pretranslateAll && pretranslateCorpus is not null)
            corpus.TrainOnAll = true;
        else if (source.TrainOnTextIds is not null)
        {
            corpus.TrainOnTextIds.Add(source.TrainOnTextIds);
        }
        else if (source.TrainOnChapters is not null)
                sourceCorpus.InferenceTextIds = pretranslateCorpus.TextIds.ToHashSet();
            }
        {
            corpus.TrainOnChapters.Add(
                source
                    .TrainOnChapters?.Select(kvp =>
                    {
                        var scriptureChapters = new ScriptureChapters();
                        scriptureChapters.Chapters.Add(kvp.Value);
                        return (kvp.Key, scriptureChapters);
                    })
                    .ToDictionary()
            );
        }
            corpus.TargetCorpora = [targetCorpus];

        if (source.PretranslateAll)
        {
            corpus.PretranslateAll = true;
        }
        else if (source.InferenceTextIds is not null)
        {
            corpus.PretranslateTextIds.Add(source.InferenceTextIds);
        else if (source.InferenceChapters is not null)
            corpus.PretranslateChapters.Add(
                source
                    .InferenceChapters?.Select(kvp =>
                    })
                    .ToDictionary()
            );
        }

        return corpus;
            {
                result.TrainOnTextIds = trainingFilter.TextIds.ToHashSet();
        }
        // null TrainOnTextIds and null TrainOnChapters means train on all
    }

    private static V1.CorpusFile Map(SIL.ServiceToolkit.Models.CorpusFile source)
    {
        return new V1.CorpusFile
            {
                result.InferenceTextIds = pretranslateFilter.TextIds.ToHashSet();
            {
            Location = source.Location,
            TextId = source.TextId,
            Format = Map(source.Format),
        };
    }
        }
        // null InferenceTextIds and null InferenceChapters means infer on all

    private static V1.FileFormat Map(SIL.ServiceToolkit.Models.FileFormat source)
    {
        return source switch
        {
            SIL.ServiceToolkit.Models.FileFormat.Text => V1.FileFormat.Text,
            SIL.ServiceToolkit.Models.FileFormat.Paratext => V1.FileFormat.Paratext,
            _ => throw new InvalidEnumArgumentException(nameof(source)),
        };
    }
}
