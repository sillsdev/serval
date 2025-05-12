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

    public override async Task<Engine> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        Engine engine = await base.GetAsync(id, cancellationToken);
        if (!(engine.IsInitialized ?? true))
            throw new EntityNotFoundException($"Could not find the {typeof(Engine).Name} '{id}'.");
        return engine;
    }

    public override async Task<IEnumerable<Engine>> GetAllAsync(
        string owner,
        CancellationToken cancellationToken = default
    )
    {
        return await Entities.GetAllAsync(
            e => e.Owner == owner && (e.IsInitialized == null || e.IsInitialized.Value),
            cancellationToken
        );
    }

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
        try
        {
            engine.DateCreated = DateTime.UtcNow;
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
            await Entities.UpdateAsync(
                engine,
                u =>
                {
                    u.Set(e => e.IsInitialized, true);
                    u.Set(e => e.IsModelPersisted, engine.IsModelPersisted);
                },
                cancellationToken: CancellationToken.None
            );
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
        return engine;
    }

    public async Task UpdateAsync(
        string engineId,
        string sourceLanguage = "",
        string targetLanguage = "",
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await Entities.GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

        if (sourceLanguage == "")
            sourceLanguage = engine.SourceLanguage;
        if (targetLanguage == "")
            targetLanguage = engine.TargetLanguage;

        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await Entities.UpdateAsync(
                    engine,
                    u =>
                    {
                        u.Set(e => e.SourceLanguage, sourceLanguage);
                        u.Set(e => e.TargetLanguage, targetLanguage);
                    },
                    cancellationToken: ct
                );
                await _pretranslations.DeleteAllAsync(pt => pt.EngineRef == engineId, ct);
            },
            CancellationToken.None
        );

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        await client.UpdateAsync(
            new UpdateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage
            },
            cancellationToken: cancellationToken
        );
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
        build.DateCreated = DateTime.UtcNow;
        Engine engine = await GetAsync(build.EngineRef, cancellationToken);
        await _builds.InsertAsync(build, cancellationToken);

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);

        try
        {
            StartBuildRequest request;
            if (engine.ParallelCorpora.Any())
            {
                Dictionary<string, TrainingCorpus>? trainOn = build.TrainOn?.ToDictionary(c => c.ParallelCorpusRef!);
                Dictionary<string, PretranslateCorpus>? pretranslate = build.Pretranslate?.ToDictionary(c =>
                    c.ParallelCorpusRef!
                );
                IReadOnlyList<Shared.Models.ParallelCorpus> parallelCorpora = engine
                    .ParallelCorpora.Where(pc =>
                        trainOn == null
                        || trainOn.ContainsKey(pc.Id)
                        || pretranslate == null
                        || pretranslate.ContainsKey(pc.Id)
                    )
                    .ToList();

                request = new StartBuildRequest
                {
                    EngineType = engine.Type,
                    EngineId = engine.Id,
                    BuildId = build.Id,
                    Corpora =
                    {
                        parallelCorpora.Select(c =>
                            Map(
                                c,
                                trainOn?.GetValueOrDefault(c.Id),
                                pretranslate?.GetValueOrDefault(c.Id),
                                trainOn is null,
                                pretranslate is null
                            )
                        )
                    }
                };
            }
            else
            {
                Dictionary<string, TrainingCorpus>? trainOn = build.TrainOn?.ToDictionary(c => c.CorpusRef!);
                Dictionary<string, PretranslateCorpus>? pretranslate = build.Pretranslate?.ToDictionary(c =>
                    c.CorpusRef!
                );
                IReadOnlyList<Corpus> corpora = engine
                    .Corpora.Where(c =>
                        trainOn == null
                        || trainOn.ContainsKey(c.Id)
                        || pretranslate == null
                        || pretranslate.ContainsKey(c.Id)
                    )
                    .ToList();

                request = new StartBuildRequest
                {
                    EngineType = engine.Type,
                    EngineId = engine.Id,
                    BuildId = build.Id,
                    Corpora =
                    {
                        corpora.Select(c =>
                            Map(
                                c,
                                trainOn?.GetValueOrDefault(c.Id),
                                pretranslate?.GetValueOrDefault(c.Id),
                                trainOn is null,
                                pretranslate is null
                            )
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
            await _builds.UpdateAsync(
                b => b.Id == build.Id,
                u => u.Set(e => e.IsInitialized, true),
                cancellationToken: CancellationToken.None
            );
        }
        catch
        {
            await _builds.DeleteAsync(build, CancellationToken.None);
            throw;
        }
    }

    public async Task<Build?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        Engine? engine = await GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        try
        {
            CancelBuildResponse cancelBuildResponse = await client.CancelBuildAsync(
                new CancelBuildRequest { EngineType = engine.Type, EngineId = engine.Id },
                cancellationToken: cancellationToken
            );
            return await _builds.GetAsync(cancelBuildResponse.BuildId, cancellationToken);
        }
        catch (RpcException re)
        {
            if (re.StatusCode is StatusCode.Aborted)
                return null;
            throw;
        }
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
        return Entities.UpdateAsync(
            e => e.Id == engineId && (e.IsInitialized == null || e.IsInitialized.Value),
            u => u.Add(e => e.Corpora, corpus),
            cancellationToken: cancellationToken
        );
    }

    public async Task<Models.Corpus> UpdateCorpusAsync(
        string engineId,
        string corpusId,
        IReadOnlyList<Shared.Models.CorpusFile>? sourceFiles,
        IReadOnlyList<Shared.Models.CorpusFile>? targetFiles,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await Entities.UpdateAsync(
            e =>
                e.Id == engineId
                && (e.IsInitialized == null || e.IsInitialized.Value)
                && e.Corpora.Any(c => c.Id == corpusId),
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
                    e => e.Id == engineId && (e.IsInitialized == null || e.IsInitialized.Value),
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
        Shared.Models.ParallelCorpus corpus,
        CancellationToken cancellationToken = default
    )
    {
        return Entities.UpdateAsync(
            e => e.Id == engineId && (e.IsInitialized == null || e.IsInitialized.Value),
            u => u.Add(e => e.ParallelCorpora, corpus),
            cancellationToken: cancellationToken
        );
    }

    public async Task<Shared.Models.ParallelCorpus> UpdateParallelCorpusAsync(
        string engineId,
        string parallelCorpusId,
        IReadOnlyList<Shared.Models.MonolingualCorpus>? sourceCorpora,
        IReadOnlyList<Shared.Models.MonolingualCorpus>? targetCorpora,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await Entities.UpdateAsync(
            e =>
                e.Id == engineId
                && (e.IsInitialized == null || e.IsInitialized.Value)
                && e.ParallelCorpora.Any(c => c.Id == parallelCorpusId),
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
                    e => e.Id == engineId && (e.IsInitialized == null || e.IsInitialized.Value),
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
                )
                || e.ParallelCorpora.Any(c =>
                    c.SourceCorpora.Any(sc => sc.Files.Any(f => f.Id == dataFileId))
                    || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                ),
            u =>
            {
                u.RemoveAll(e => e.Corpora[ArrayPosition.All].SourceFiles, f => f.Id == dataFileId);
                u.RemoveAll(e => e.Corpora[ArrayPosition.All].TargetFiles, f => f.Id == dataFileId);
                u.RemoveAll(
                    e => e.ParallelCorpora[ArrayPosition.All].SourceCorpora[ArrayPosition.All].Files,
                    f => f.Id == dataFileId
                );
                u.RemoveAll(
                    e => e.ParallelCorpora[ArrayPosition.All].TargetCorpora[ArrayPosition.All].Files,
                    f => f.Id == dataFileId
                );
            },
            cancellationToken: cancellationToken
        );
    }

    public Task UpdateDataFileFilenameFilesAsync(
        string dataFileId,
        string filename,
        CancellationToken cancellationToken = default
    )
    {
        return Entities.UpdateAllAsync(
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
                    e => e.Corpora[ArrayPosition.All].SourceFiles,
                    f => f.Filename,
                    filename,
                    f => f.Id == dataFileId
                );
                u.SetAll(
                    e => e.Corpora[ArrayPosition.All].TargetFiles,
                    f => f.Filename,
                    filename,
                    f => f.Id == dataFileId
                );
                u.SetAll(
                    e => e.ParallelCorpora[ArrayPosition.All].SourceCorpora[ArrayPosition.All].Files,
                    f => f.Filename,
                    filename,
                    f => f.Id == dataFileId
                );
                u.SetAll(
                    e => e.ParallelCorpora[ArrayPosition.All].TargetCorpora[ArrayPosition.All].Files,
                    f => f.Filename,
                    filename,
                    f => f.Id == dataFileId
                );
            },
            cancellationToken: cancellationToken
        );
    }

    public Task UpdateCorpusFilesAsync(
        string corpusId,
        IReadOnlyList<Shared.Models.CorpusFile> files,
        CancellationToken cancellationToken = default
    )
    {
        return Entities.UpdateAllAsync(
            e =>
                e.ParallelCorpora.Any(c =>
                    c.SourceCorpora.Any(sc => sc.Id == corpusId) || c.TargetCorpora.Any(tc => tc.Id == corpusId)
                ),
            u =>
            {
                u.SetAll(
                    e => e.ParallelCorpora[ArrayPosition.All].SourceCorpora,
                    mc => mc.Files,
                    files,
                    mc => mc.Id == corpusId
                );
                u.SetAll(
                    e => e.ParallelCorpora[ArrayPosition.All].TargetCorpora,
                    mc => mc.Files,
                    files,
                    mc => mc.Id == corpusId
                );
            },
            cancellationToken: cancellationToken
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

    private V1.ParallelCorpus Map(
        Corpus source,
        TrainingCorpus? trainingCorpus,
        PretranslateCorpus? pretranslateCorpus,
        bool trainOnAllCorpora,
        bool pretranslateOnAllCorpora
    )
    {
        IEnumerable<V1.CorpusFile> sourceFiles = source.SourceFiles.Select(Map);
        IEnumerable<V1.CorpusFile> targetFiles = source.TargetFiles.Select(Map);
        V1.MonolingualCorpus sourceCorpus =
            new() { Language = source.SourceLanguage, Files = { source.SourceFiles.Select(Map) } };
        V1.MonolingualCorpus targetCorpus =
            new() { Language = source.TargetLanguage, Files = { source.TargetFiles.Select(Map) } };

        if (
            trainOnAllCorpora
            || (trainingCorpus is not null && trainingCorpus.TextIds is null && trainingCorpus.ScriptureRange is null)
        )
        {
            sourceCorpus.TrainOnAll = true;
            targetCorpus.TrainOnAll = true;
        }
        else if (trainingCorpus is not null)
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
            pretranslateOnAllCorpora
            || (
                pretranslateCorpus is not null
                && pretranslateCorpus.TextIds is null
                && pretranslateCorpus.ScriptureRange is null
            )
        )
        {
            sourceCorpus.PretranslateAll = true;
            targetCorpus.PretranslateAll = true;
        }
        else if (pretranslateCorpus is not null)
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
        Shared.Models.ParallelCorpus source,
        TrainingCorpus? trainingCorpus,
        PretranslateCorpus? pretranslateCorpus,
        bool trainOnAllCorpora,
        bool pretranslateOnAllCorpora
    )
    {
        string? referenceFileLocation =
            source.TargetCorpora.Count > 0 && source.TargetCorpora[0].Files.Count > 0
                ? Map(source.TargetCorpora[0].Files[0]).Location
                : null;

        bool trainOnAllSources =
            trainOnAllCorpora || (trainingCorpus is not null && trainingCorpus.SourceFilters is null);
        bool pretranslateAllSources =
            pretranslateOnAllCorpora || (pretranslateCorpus is not null && pretranslateCorpus.SourceFilters is null);

        bool trainOnAllTargets =
            trainOnAllCorpora || (trainingCorpus is not null && trainingCorpus.TargetFilters is null);
        bool pretranslateAllTargets = pretranslateOnAllCorpora || pretranslateCorpus is not null; // there is no pretranslate Target filter.

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
                        referenceFileLocation,
                        trainOnAllSources,
                        pretranslateAllSources
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
                        referenceFileLocation,
                        trainOnAllTargets,
                        pretranslateAllTargets
                    )
                )
            }
        };
    }

    private V1.MonolingualCorpus Map(
        Shared.Models.MonolingualCorpus inputCorpus,
        ParallelCorpusFilter? trainingFilter,
        ParallelCorpusFilter? pretranslateFilter,
        string? referenceFileLocation,
        bool trainOnAll,
        bool pretranslateOnAll
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
            pretranslateChapters = GetChapters(referenceFileLocation, pretranslateFilter.ScriptureRange)
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

        var returnCorpus = new V1.MonolingualCorpus
        {
            Id = inputCorpus.Id,
            Language = inputCorpus.Language,
            Files = { inputCorpus.Files.Select(Map) }
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

        if (
            trainOnAll
            || (trainingFilter is not null && trainingFilter.TextIds is null && trainingFilter.ScriptureRange is null)
        )
        {
            returnCorpus.TrainOnAll = true;
        }
        else
        {
            if (trainOnChapters is not null)
                returnCorpus.TrainOnChapters.Add(trainOnChapters);
            if (trainingFilter?.TextIds is not null)
                returnCorpus.TrainOnTextIds.Add(trainingFilter.TextIds);
        }

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

        if (
            pretranslateOnAll
            || (
                pretranslateFilter is not null
                && pretranslateFilter.TextIds is null
                && pretranslateFilter.ScriptureRange is null
            )
        )
        {
            returnCorpus.PretranslateAll = true;
        }
        else
        {
            if (pretranslateChapters is not null)
                returnCorpus.PretranslateChapters.Add(pretranslateChapters);
            if (pretranslateFilter?.TextIds is not null)
                returnCorpus.PretranslateTextIds.Add(pretranslateFilter.TextIds);
        }

        return returnCorpus;
    }

    private V1.CorpusFile Map(Shared.Models.CorpusFile source)
    {
        return new V1.CorpusFile
        {
            TextId = source.TextId,
            Format = (V1.FileFormat)source.Format,
            Location = Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, source.Filename)
        };
    }
}
