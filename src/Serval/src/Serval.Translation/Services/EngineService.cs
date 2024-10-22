using MassTransit.Mediator;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

public class EngineService(
    IRepository<Engine> engines,
    IRepository<Build> builds,
    IRepository<Pretranslation> pretranslations,
    IScopedMediator mediator,
    GrpcClientFactory grpcClientFactory,
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory,
    IScriptureDataFileService scriptureDataFileService
) : OwnedEntityServiceBase<Engine>(engines), IEngineService
{
    private readonly IRepository<Build> _builds = builds;
    private readonly IRepository<Pretranslation> _pretranslations = pretranslations;
    private readonly IScopedMediator _mediator = mediator;
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly ILogger<EngineService> _logger = loggerFactory.CreateLogger<EngineService>();
    private readonly IScriptureDataFileService _scriptureDataFileService = scriptureDataFileService;

    public async Task<Models.TranslationResult> TranslateAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        TranslateResponse response = await client.TranslateAsync(
            new TranslateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                N = 1,
                Segment = segment
            },
            cancellationToken: cancellationToken
        );
        return Map(response.Results[0]);
    }

    public async Task<IEnumerable<Models.TranslationResult>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        TranslateResponse response = await client.TranslateAsync(
            new TranslateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                N = n,
                Segment = segment
            },
            cancellationToken: cancellationToken
        );
        return response.Results.Select(Map);
    }

    public async Task<Models.WordGraph> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        GetWordGraphResponse response = await client.GetWordGraphAsync(
            new GetWordGraphRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                Segment = segment
            },
            cancellationToken: cancellationToken
        );
        return Map(response.WordGraph);
    }

    public async Task TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        await client.TrainSegmentPairAsync(
            new TrainSegmentPairRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                SourceSegment = sourceSegment,
                TargetSegment = targetSegment,
                SentenceStart = sentenceStart
            },
            cancellationToken: cancellationToken
        );
    }

    public override async Task<Engine> CreateAsync(Engine engine, CancellationToken cancellationToken = default)
    {
        bool updateIsModelPersisted = engine.IsModelPersisted is null;
        try
        {
            await Entities.InsertAsync(engine, cancellationToken);
            TranslationEngineApi.TranslationEngineApiClient? client =
                _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
            if (client is null)
                throw new InvalidOperationException($"'{engine.Type}' is an invalid engine type.");
            var request = new CreateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                SourceLanguage = engine.SourceLanguage,
                TargetLanguage = engine.TargetLanguage
            };
            if (engine.IsModelPersisted is not null)
                request.IsModelPersisted = engine.IsModelPersisted.Value;

            if (engine.Name is not null)
                request.EngineName = engine.Name;
            CreateResponse createResponse = await client.CreateAsync(request, cancellationToken: cancellationToken);
            // IsModelPersisted may be updated by the engine with the respective default.
            engine = engine with
            {
                IsModelPersisted = createResponse.IsModelPersisted
            };
        }
        catch (RpcException rpcex)
        {
            await Entities.DeleteAsync(engine, CancellationToken.None);
            if (rpcex.StatusCode == StatusCode.InvalidArgument)
            {
                throw new InvalidOperationException(
                    $"Unable to create engine {engine.Id} because of an invalid argument: {rpcex.Status.Detail}",
                    rpcex
                );
            }
            throw;
        }
        catch
        {
            await Entities.DeleteAsync(engine, CancellationToken.None);
            throw;
        }
        if (updateIsModelPersisted)
        {
            await Entities.UpdateAsync(
                engine,
                u => u.Set(e => e.IsModelPersisted, engine.IsModelPersisted),
                cancellationToken: cancellationToken
            );
        }
        return engine;
    }

    public override async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        Engine? engine = await Entities.GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        await client.DeleteAsync(
            new DeleteRequest { EngineType = engine.Type, EngineId = engine.Id },
            cancellationToken: cancellationToken
        );

        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await Entities.DeleteAsync(engineId, ct);
                await _builds.DeleteAllAsync(b => b.EngineRef == engineId, ct);
                await _pretranslations.DeleteAllAsync(pt => pt.EngineRef == engineId, ct);
            },
            CancellationToken.None
        );
    }

    private Dictionary<string, List<int>> GetChapters(string fileLocation, string scriptureRange)
    {
        try
        {
            return ScriptureRangeParser.GetChapters(
                scriptureRange,
                _scriptureDataFileService.GetParatextProjectSettings(fileLocation).Versification
            );
        }
        catch (ArgumentException ae)
        {
            throw new InvalidOperationException($"The scripture range {scriptureRange} is not valid: {ae.Message}");
        }
    }

    public async Task StartBuildAsync(Build build, CancellationToken cancellationToken = default)
    {
        Engine engine = await GetAsync(build.EngineRef, cancellationToken);
        await _builds.InsertAsync(build, cancellationToken);

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);

        try
        {
            StartBuildRequest request;
            if (engine.ParallelCorpora.Any())
            {
                var trainOn = build.TrainOn?.ToDictionary(c => c.ParallelCorpusRef!);
                var pretranslate = build.Pretranslate?.ToDictionary(c => c.ParallelCorpusRef!);
                request = new StartBuildRequest
                {
                    EngineType = engine.Type,
                    EngineId = engine.Id,
                    BuildId = build.Id,
                    Corpora =
                    {
                        engine.ParallelCorpora.Select(c =>
                            Map(c, trainOn?.GetValueOrDefault(c.Id), pretranslate?.GetValueOrDefault(c.Id))
                        )
                    }
                };
            }
            else
            {
                var pretranslate = build.Pretranslate?.ToDictionary(c => c.CorpusRef!);
                var trainOn = build.TrainOn?.ToDictionary(c => c.CorpusRef!);

                request = new StartBuildRequest
                {
                    EngineType = engine.Type,
                    EngineId = engine.Id,
                    BuildId = build.Id,
                    Corpora =
                    {
                        engine.Corpora.Select(c =>
                            Map(c, trainOn?.GetValueOrDefault(c.Id), pretranslate?.GetValueOrDefault(c.Id))
                        )
                    }
                };
            }

            if (build.Options is not null)
                request.Options = JsonSerializer.Serialize(build.Options);

            // Log the build request summary
            try
            {
                var buildRequestSummary = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(request))!;
                // correct build options parsing
                buildRequestSummary.Remove("Options");
                try
                {
                    buildRequestSummary.Add("Options", JsonNode.Parse(request.Options));
                }
                catch (JsonException)
                {
                    buildRequestSummary.Add(
                        "Options",
                        "Build \"Options\" failed parsing: " + (request.Options ?? "null")
                    );
                }
                buildRequestSummary.Add("Event", "BuildRequest");
                buildRequestSummary.Add("ModelRevision", engine.ModelRevision);
                buildRequestSummary.Add("ClientId", engine.Owner);
                _logger.LogInformation("{request}", buildRequestSummary.ToJsonString());
            }
            catch (JsonException)
            {
                _logger.LogInformation("Error parsing build request summary.");
                _logger.LogInformation("{request}", JsonSerializer.Serialize(request));
            }
            await client.StartBuildAsync(request, cancellationToken: cancellationToken);
        }
        catch
        {
            await _builds.DeleteAsync(build, CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        Engine? engine = await GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        try
        {
            await client.CancelBuildAsync(
                new CancelBuildRequest { EngineType = engine.Type, EngineId = engine.Id },
                cancellationToken: cancellationToken
            );
        }
        catch (RpcException re)
        {
            if (re.StatusCode is StatusCode.Aborted)
                return false;
            throw;
        }
        return true;
    }

    public async Task<ModelDownloadUrl> GetModelDownloadUrlAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        GetModelDownloadUrlResponse result = await client.GetModelDownloadUrlAsync(
            new GetModelDownloadUrlRequest { EngineType = engine.Type, EngineId = engine.Id },
            cancellationToken: cancellationToken
        );
        return new ModelDownloadUrl
        {
            Url = result.Url,
            ModelRevision = result.ModelRevision,
            ExpiresAt = result.ExpiresAt.ToDateTime()
        };
    }

    public Task AddCorpusAsync(string engineId, Models.Corpus corpus, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAsync(engineId, u => u.Add(e => e.Corpora, corpus), cancellationToken: cancellationToken);
    }

    public async Task<Models.Corpus> UpdateCorpusAsync(
        string engineId,
        string corpusId,
        IReadOnlyList<Models.CorpusFile>? sourceFiles,
        IReadOnlyList<Models.CorpusFile>? targetFiles,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await Entities.UpdateAsync(
            e => e.Id == engineId && e.Corpora.Any(c => c.Id == corpusId),
            u =>
            {
                if (sourceFiles is not null)
                    u.Set(c => c.Corpora[ArrayPosition.FirstMatching].SourceFiles, sourceFiles);
                if (targetFiles is not null)
                    u.Set(c => c.Corpora[ArrayPosition.FirstMatching].TargetFiles, targetFiles);
            },
            cancellationToken: cancellationToken
        );
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Corpus '{corpusId}' in Engine '{engineId}'.");
        return engine.Corpora.First(c => c.Id == corpusId);
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
                    engineId,
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
        Models.ParallelCorpus corpus,
        CancellationToken cancellationToken = default
    )
    {
        return Entities.UpdateAsync(
            engineId,
            u => u.Add(e => e.ParallelCorpora, corpus),
            cancellationToken: cancellationToken
        );
    }

    public async Task<Models.ParallelCorpus> UpdateParallelCorpusAsync(
        string engineId,
        string parallelCorpusId,
        IReadOnlyList<Models.MonolingualCorpus>? sourceCorpora,
        IReadOnlyList<Models.MonolingualCorpus>? targetCorpora,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await Entities.UpdateAsync(
            e => e.Id == engineId && e.ParallelCorpora.Any(c => c.Id == parallelCorpusId),
            u =>
            {
                if (sourceCorpora is not null)
                    u.Set(c => c.ParallelCorpora[ArrayPosition.FirstMatching].SourceCorpora, sourceCorpora);
                if (targetCorpora is not null)
                    u.Set(c => c.ParallelCorpora[ArrayPosition.FirstMatching].TargetCorpora, targetCorpora);
            },
            cancellationToken: cancellationToken
        );
        if (engine is null)
        {
            throw new EntityNotFoundException(
                $"Could not find the Corpus '{parallelCorpusId}' in Engine '{engineId}'."
            );
        }

        return engine.ParallelCorpora.First(c => c.Id == parallelCorpusId);
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
                    engineId,
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

    public Task DeleteAllCorpusFilesAsync(string dataFileId, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAllAsync(
            e =>
                e.Corpora.Any(c =>
                    c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                ),
            u =>
                u.RemoveAll(e => e.Corpora[ArrayPosition.All].SourceFiles, f => f.Id == dataFileId)
                    .RemoveAll(e => e.Corpora[ArrayPosition.All].TargetFiles, f => f.Id == dataFileId),
            cancellationToken
        );
    }

    public async Task<Queue> GetQueueAsync(string engineType, CancellationToken cancellationToken = default)
    {
        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engineType);
        GetQueueSizeResponse response = await client.GetQueueSizeAsync(
            new GetQueueSizeRequest { EngineType = engineType },
            cancellationToken: cancellationToken
        );
        return new Queue { Size = response.Size, EngineType = engineType };
    }

    public async Task<LanguageInfo> GetLanguageInfoAsync(
        string engineType,
        string language,
        CancellationToken cancellationToken = default
    )
    {
        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engineType);
        GetLanguageInfoResponse response = await client.GetLanguageInfoAsync(
            new GetLanguageInfoRequest { EngineType = engineType, Language = language },
            cancellationToken: cancellationToken
        );
        return new LanguageInfo
        {
            InternalCode = response.InternalCode,
            IsNative = response.IsNative,
            EngineType = engineType
        };
    }

    private Models.TranslationResult Map(V1.TranslationResult source)
    {
        return new Models.TranslationResult
        {
            Translation = source.Translation,
            SourceTokens = source.SourceTokens.ToList(),
            TargetTokens = source.TargetTokens.ToList(),
            Confidences = source.Confidences.ToList(),
            Sources = source.Sources.Select(Map).ToList(),
            Alignment = source.Alignment.Select(Map).ToList(),
            Phrases = source.Phrases.Select(Map).ToList()
        };
    }

    private IReadOnlySet<Contracts.TranslationSource> Map(TranslationSources source)
    {
        return source.Values.Cast<Contracts.TranslationSource>().ToHashSet();
    }

    private Models.AlignedWordPair Map(V1.AlignedWordPair source)
    {
        return new Models.AlignedWordPair { SourceIndex = source.SourceIndex, TargetIndex = source.TargetIndex };
    }

    private Models.Phrase Map(V1.Phrase source)
    {
        return new Models.Phrase
        {
            SourceSegmentStart = source.SourceSegmentStart,
            SourceSegmentEnd = source.SourceSegmentEnd,
            TargetSegmentCut = source.TargetSegmentCut
        };
    }

    private Models.WordGraph Map(V1.WordGraph source)
    {
        return new Models.WordGraph
        {
            SourceTokens = source.SourceTokens.ToList(),
            InitialStateScore = source.InitialStateScore,
            FinalStates = source.FinalStates.ToHashSet(),
            Arcs = source.Arcs.Select(Map).ToList()
        };
    }

    private Models.WordGraphArc Map(V1.WordGraphArc source)
    {
        return new Models.WordGraphArc
        {
            PrevState = source.PrevState,
            NextState = source.NextState,
            Score = source.Score,
            TargetTokens = source.TargetTokens.ToList(),
            Confidences = source.Confidences.ToList(),
            SourceSegmentStart = source.SourceSegmentStart,
            SourceSegmentEnd = source.SourceSegmentEnd,
            Alignment = source.Alignment.Select(Map).ToList(),
            Sources = source.Sources.Select(Map).ToList()
        };
    }

    private V1.ParallelCorpus Map(Corpus source, TrainingCorpus? trainingCorpus, PretranslateCorpus? pretranslateCorpus)
    {
        IEnumerable<V1.CorpusFile> sourceFiles = source.SourceFiles.Select(Map);
        IEnumerable<V1.CorpusFile> targetFiles = source.TargetFiles.Select(Map);
        V1.MonolingualCorpus sourceCorpus =
            new() { Language = source.SourceLanguage, Files = { source.SourceFiles.Select(Map) } };
        V1.MonolingualCorpus targetCorpus =
            new() { Language = source.TargetLanguage, Files = { source.TargetFiles.Select(Map) } };

        if (trainingCorpus is null || (trainingCorpus.TextIds is null && trainingCorpus.ScriptureRange is null))
        {
            sourceCorpus.TrainOnAll = true;
            targetCorpus.TrainOnAll = true;
        }
        else
        {
            if (trainingCorpus.TextIds is not null && trainingCorpus.ScriptureRange is not null)
            {
                throw new InvalidOperationException(
                    $"The corpus {source.Id} cannot specify both 'textIds' and 'scriptureRange' for trainOn"
                );
            }
            if (trainingCorpus.TextIds is not null)
            {
                sourceCorpus.TrainOnTextIds.Add(trainingCorpus.TextIds);
                targetCorpus.TrainOnTextIds.Add(trainingCorpus.TextIds);
            }
            if (!string.IsNullOrEmpty(trainingCorpus.ScriptureRange))
            {
                if (targetCorpus.Files.Count > 1 || targetCorpus.Files[0].Format != V1.FileFormat.Paratext)
                {
                    throw new InvalidOperationException(
                        $"The corpus {source.Id} is not compatible with using a scripture range"
                    );
                }
                var chapters = GetChapters(targetCorpus.Files[0].Location, trainingCorpus.ScriptureRange)
                    .Select(
                        (kvp) =>
                        {
                            var scriptureChapters = new ScriptureChapters();
                            scriptureChapters.Chapters.Add(kvp.Value);
                            return (kvp.Key, scriptureChapters);
                        }
                    )
                    .ToDictionary();
                sourceCorpus.TrainOnChapters.Add(chapters);
                targetCorpus.TrainOnChapters.Add(chapters);
            }
        }
        if (
            pretranslateCorpus is null
            || (pretranslateCorpus.TextIds is null && pretranslateCorpus.ScriptureRange is null)
        )
        {
            sourceCorpus.PretranslateAll = true;
            targetCorpus.PretranslateAll = true;
        }
        else
        {
            if (pretranslateCorpus.TextIds is not null && pretranslateCorpus.ScriptureRange is not null)
            {
                throw new InvalidOperationException(
                    $"The corpus {source.Id} cannot specify both 'textIds' and 'scriptureRange' for 'pretranslate'."
                );
            }
            if (pretranslateCorpus.TextIds is not null)
                sourceCorpus.PretranslateTextIds.Add(pretranslateCorpus.TextIds);
            if (!string.IsNullOrEmpty(pretranslateCorpus.ScriptureRange))
            {
                if (targetCorpus.Files.Count > 1 || targetCorpus.Files[0].Format != V1.FileFormat.Paratext)
                {
                    throw new InvalidOperationException(
                        $"The corpus {source.Id} is not compatible with using a scripture range"
                    );
                }
                sourceCorpus.PretranslateChapters.Add(
                    GetChapters(targetCorpus.Files[0].Location, pretranslateCorpus.ScriptureRange)
                        .Select(
                            (kvp) =>
                            {
                                var scriptureChapters = new ScriptureChapters();
                                scriptureChapters.Chapters.Add(kvp.Value);
                                return (kvp.Key, scriptureChapters);
                            }
                        )
                        .ToDictionary()
                );
            }
        }
        V1.ParallelCorpus corpus = new() { Id = source.Id };
        if (sourceCorpus.Files.Count > 0)
            corpus.SourceCorpora.Add(sourceCorpus);
        if (targetCorpus.Files.Count > 0)
            corpus.TargetCorpora.Add(targetCorpus);
        return corpus;
    }

    private V1.ParallelCorpus Map(
        Models.ParallelCorpus source,
        TrainingCorpus? trainingCorpus,
        PretranslateCorpus? pretranslateCorpus
    )
    {
        string? referenceFileLocation =
            source.TargetCorpora.Count > 0 && source.TargetCorpora[0].Files.Count > 0
                ? Map(source.TargetCorpora[0].Files[0]).Location
                : null;

        return new V1.ParallelCorpus
        {
            Id = source.Id,
            SourceCorpora =
            {
                source.SourceCorpora.Select(sc =>
                    Map(
                        sc,
                        trainingCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                        pretranslateCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                        referenceFileLocation
                    )
                )
            },
            TargetCorpora =
            {
                source.TargetCorpora.Select(tc =>
                    Map(
                        tc,
                        trainingCorpus?.TargetFilters?.Where(sf => sf.CorpusRef == tc.Id).FirstOrDefault(),
                        null,
                        referenceFileLocation
                    )
                )
            }
        };
    }

    private V1.MonolingualCorpus Map(
        Models.MonolingualCorpus source,
        ParallelCorpusFilter? trainingFilter,
        ParallelCorpusFilter? pretranslateFilter,
        string? referenceFileLocation
    )
    {
        Dictionary<string, ScriptureChapters>? trainOnChapters = null;
        if (
            trainingFilter is not null
            && trainingFilter.ScriptureRange is not null
            && referenceFileLocation is not null
        )
        {
            trainOnChapters = GetChapters(referenceFileLocation, trainingFilter.ScriptureRange)
                .Select(
                    (kvp) =>
                    {
                        var scriptureChapters = new ScriptureChapters();
                        scriptureChapters.Chapters.Add(kvp.Value);
                        return (kvp.Key, scriptureChapters);
                    }
                )
                .ToDictionary();
        }

        Dictionary<string, ScriptureChapters>? pretranslateChapters = null;
        if (
            pretranslateFilter is not null
            && pretranslateFilter.ScriptureRange is not null
            && referenceFileLocation is not null
        )
        {
            GetChapters(referenceFileLocation, pretranslateFilter.ScriptureRange)
                .Select(
                    (kvp) =>
                    {
                        var scriptureChapters = new ScriptureChapters();
                        scriptureChapters.Chapters.Add(kvp.Value);
                        return (kvp.Key, scriptureChapters);
                    }
                )
                .ToDictionary();
        }

        var corpus = new V1.MonolingualCorpus
        {
            Id = source.Id,
            Language = source.Language,
            Files = { source.Files.Select(Map) }
        };

        if (trainingFilter is null || (trainingFilter.TextIds is null && trainingFilter.ScriptureRange is null))
        {
            corpus.TrainOnAll = true;
        }
        else
        {
            if (trainOnChapters is not null)
                corpus.TrainOnChapters.Add(trainOnChapters);
            if (trainingFilter?.TextIds is not null)
                corpus.TrainOnTextIds.Add(trainingFilter.TextIds);
        }

        if (
            pretranslateFilter is null
            || (pretranslateFilter.TextIds is null && pretranslateFilter.ScriptureRange is null)
        )
        {
            corpus.PretranslateAll = true;
        }
        else
        {
            if (pretranslateChapters is not null)
                corpus.PretranslateChapters.Add(pretranslateChapters);
            if (pretranslateFilter?.TextIds is not null)
                corpus.PretranslateTextIds.Add(pretranslateFilter.TextIds);
        }

        return corpus;
    }

    private V1.CorpusFile Map(Models.CorpusFile source)
    {
        return new V1.CorpusFile
        {
            TextId = source.TextId,
            Format = (V1.FileFormat)source.Format,
            Location = Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, source.Filename)
        };
    }
}
