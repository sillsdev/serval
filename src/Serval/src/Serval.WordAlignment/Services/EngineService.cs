using MassTransit.Mediator;
using Serval.WordAlignment.V1;

namespace Serval.WordAlignment.Services;

public class EngineService(
    IRepository<Engine> engines,
    IRepository<Build> builds,
    IRepository<Models.WordAlignment> wordAlignments,
    IScopedMediator mediator,
    GrpcClientFactory grpcClientFactory,
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory,
    IScriptureDataFileService scriptureDataFileService
) : OwnedEntityServiceBase<Engine>(engines), IEngineService
{
    private readonly IRepository<Build> _builds = builds;
    private readonly IRepository<Models.WordAlignment> _wordAlignments = wordAlignments;
    private readonly IScopedMediator _mediator = mediator;
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly ILogger<EngineService> _logger = loggerFactory.CreateLogger<EngineService>();
    private readonly IScriptureDataFileService _scriptureDataFileService = scriptureDataFileService;

    public async Task<Models.WordAlignmentResult> GetWordAlignmentAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await GetAsync(engineId, cancellationToken);

        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(engine.Type);
        GetWordAlignmentResponse response = await client.GetWordAlignmentAsync(
            new GetWordAlignmentRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                Segment = segment
            },
            cancellationToken: cancellationToken
        );
        return Map(response.Result);
    }

    public override async Task<Engine> CreateAsync(Engine engine, CancellationToken cancellationToken = default)
    {
        bool updateIsModelPersisted = engine.IsModelPersisted is null;
        try
        {
            await Entities.InsertAsync(engine, cancellationToken);
            WordAlignmentEngineApi.WordAlignmentEngineApiClient? client =
                _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(engine.Type);
            if (client is null)
                throw new InvalidOperationException($"'{engine.Type}' is an invalid engine type.");
            var request = new CreateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                SourceLanguage = engine.SourceLanguage,
                TargetLanguage = engine.TargetLanguage
            };

            if (engine.Name is not null)
                request.EngineName = engine.Name;
            await client.CreateAsync(request, cancellationToken: cancellationToken);
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

        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(engine.Type);
        await client.DeleteAsync(
            new DeleteRequest { EngineType = engine.Type, EngineId = engine.Id },
            cancellationToken: cancellationToken
        );

        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await Entities.DeleteAsync(engineId, ct);
                await _builds.DeleteAllAsync(b => b.EngineRef == engineId, ct);
                await _wordAlignments.DeleteAllAsync(pt => pt.EngineRef == engineId, ct);
            },
            CancellationToken.None
        );
    }

    public async Task StartBuildAsync(Build build, CancellationToken cancellationToken = default)
    {
        Engine engine = await GetAsync(build.EngineRef, cancellationToken);
        await _builds.InsertAsync(build, cancellationToken);

        try
        {
            var wordAlignOn = build.WordAlignOn?.ToDictionary(c => c.CorpusRef);
            var trainOn = build.TrainOn?.ToDictionary(c => c.CorpusRef);
            WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
                _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(engine.Type);
            Dictionary<string, List<int>> GetChapters(V1.Corpus corpus, string scriptureRange)
            {
                try
                {
                    return ScriptureRangeParser.GetChapters(
                        scriptureRange,
                        _scriptureDataFileService
                            .GetParatextProjectSettings(corpus.TargetFiles.First().Location)
                            .Versification
                    );
                }
                catch (ArgumentException ae)
                {
                    throw new InvalidOperationException(
                        $"The scripture range {scriptureRange} is not valid: {ae.Message}"
                    );
                }
            }
            var request = new StartBuildRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                BuildId = build.Id,
                Corpora =
                {
                    engine.Corpora.Select(c =>
                    {
                        V1.Corpus corpus = Map(c);
                        if (wordAlignOn?.TryGetValue(c.Id, out WordAlignmentCorpus? wordAlignmentCorpus) ?? false)
                        {
                            corpus.WordAlignOnAll =
                                wordAlignmentCorpus.TextIds is null && wordAlignmentCorpus.ScriptureRange is null;
                            if (
                                wordAlignmentCorpus.TextIds is not null
                                && wordAlignmentCorpus.ScriptureRange is not null
                            )
                            {
                                throw new InvalidOperationException(
                                    $"The corpus {c.Id} cannot specify both 'textIds' and 'scriptureRange' for 'pretranslate'."
                                );
                            }
                            if (wordAlignmentCorpus.TextIds is not null)
                                corpus.WordAlignOnTextIds.Add(wordAlignmentCorpus.TextIds);
                            if (!string.IsNullOrEmpty(wordAlignmentCorpus.ScriptureRange))
                            {
                                if (
                                    c.TargetFiles.Count > 1
                                    || c.TargetFiles[0].Format != Shared.Contracts.FileFormat.Paratext
                                )
                                {
                                    throw new InvalidOperationException(
                                        $"The corpus {c.Id} is not compatible with using a scripture range"
                                    );
                                }
                                corpus.WordAlignOnChapters.Add(
                                    GetChapters(corpus, wordAlignmentCorpus.ScriptureRange)
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
                        if (trainOn?.TryGetValue(c.Id, out TrainingCorpus? trainingCorpus) ?? false)
                        {
                            corpus.TrainOnAll = trainingCorpus.TextIds is null && trainingCorpus.ScriptureRange is null;
                            if (trainingCorpus.TextIds is not null && trainingCorpus.ScriptureRange is not null)
                            {
                                throw new InvalidOperationException(
                                    $"The corpus {c.Id} cannot specify both 'textIds' and 'scriptureRange' for trainOn"
                                );
                            }
                            if (trainingCorpus.TextIds is not null)
                                corpus.TrainOnTextIds.Add(trainingCorpus.TextIds);
                            if (!string.IsNullOrEmpty(trainingCorpus.ScriptureRange))
                            {
                                if (
                                    c.TargetFiles.Count > 1
                                    || c.TargetFiles[0].Format != Shared.Contracts.FileFormat.Paratext
                                )
                                {
                                    throw new InvalidOperationException(
                                        $"The corpus {c.Id} is not compatible with using a scripture range"
                                    );
                                }
                                corpus.TrainOnChapters.Add(
                                    GetChapters(corpus, trainingCorpus.ScriptureRange)
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
                        else if (trainOn is null)
                        {
                            corpus.TrainOnAll = true;
                        }
                        return corpus;
                    })
                }
            };
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

        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(engine.Type);
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

    public Task AddCorpusAsync(string engineId, Models.Corpus corpus, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAsync(engineId, u => u.Add(e => e.Corpora, corpus), cancellationToken: cancellationToken);
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
                await _wordAlignments.DeleteAllAsync(pt => pt.CorpusRef == corpusId, ct);
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
        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(engineType);
        GetQueueSizeResponse response = await client.GetQueueSizeAsync(
            new GetQueueSizeRequest { EngineType = engineType },
            cancellationToken: cancellationToken
        );
        return new Queue { Size = response.Size, EngineType = engineType };
    }

    private Models.WordAlignmentResult Map(V1.WordAlignmentResult source)
    {
        return new Models.WordAlignmentResult
        {
            SourceTokens = source.SourceTokens.ToList(),
            TargetTokens = source.TargetTokens.ToList(),
            Confidences = source.Confidences.ToList(),
            Alignment = source.Alignment.Select(Map).ToList(),
        };
    }

    private Shared.Models.AlignedWordPair Map(V1.AlignedWordPair source)
    {
        return new Shared.Models.AlignedWordPair { SourceIndex = source.SourceIndex, TargetIndex = source.TargetIndex };
    }

    private V1.Corpus Map(Models.Corpus source)
    {
        return new V1.Corpus
        {
            Id = source.Id,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = { source.SourceFiles.Select(Map) },
            TargetFiles = { source.TargetFiles.Select(Map) }
        };
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
