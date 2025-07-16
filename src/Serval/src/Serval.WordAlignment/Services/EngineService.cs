﻿using MongoDB.Driver.Linq;
using Serval.WordAlignment.V1;

namespace Serval.WordAlignment.Services;

public class EngineService(
    IRepository<Engine> engines,
    IRepository<Build> builds,
    IRepository<Models.WordAlignment> wordAlignments,
    GrpcClientFactory grpcClientFactory,
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory,
    IScriptureDataFileService scriptureDataFileService
) : OwnedEntityServiceBase<Engine>(engines), IEngineService
{
    private readonly IRepository<Build> _builds = builds;
    private readonly IRepository<Models.WordAlignment> _wordAlignments = wordAlignments;
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

    public async Task<Models.WordAlignmentResult> GetWordAlignmentAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
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
                SourceSegment = sourceSegment,
                TargetSegment = targetSegment
            },
            cancellationToken: cancellationToken
        );
        return Map(response.Result);
    }

    public override async Task<Engine> CreateAsync(Engine engine, CancellationToken cancellationToken = default)
    {
        try
        {
            engine.DateCreated = DateTime.UtcNow;
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
            await Entities.UpdateAsync(
                engine,
                u => u.Set(e => e.IsInitialized, true),
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
                await _wordAlignments.DeleteAllAsync(wa => wa.EngineRef == engineId, ct);
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

        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(engine.Type);

        try
        {
            StartBuildRequest request;
            Dictionary<string, TrainingCorpus>? trainOn = build.TrainOn?.ToDictionary(c => c.ParallelCorpusRef!);
            Dictionary<string, WordAlignmentCorpus>? wordAlignOn = build.WordAlignOn?.ToDictionary(c =>
                c.ParallelCorpusRef!
            );
            IReadOnlyList<Shared.Models.ParallelCorpus> parallelCorpora = engine
                .ParallelCorpora.Where(pc =>
                    trainOn == null
                    || trainOn.ContainsKey(pc.Id)
                    || wordAlignOn == null
                    || wordAlignOn.ContainsKey(pc.Id)
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
                            wordAlignOn?.GetValueOrDefault(c.Id),
                            trainOn is null,
                            wordAlignOn is null
                        )
                    )
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

        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(engine.Type);
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
        Shared.Models.ParallelCorpus? parallelCorpus = null;
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? engine = await Entities.UpdateAsync(
                    e =>
                        e.Id == engineId
                        && (e.IsInitialized == null || e.IsInitialized.Value)
                        && e.ParallelCorpora.Any(c => c.Id == parallelCorpusId),
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
        IReadOnlyList<Shared.Models.CorpusFile> files,
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
            Alignment = source.Alignment.Select(Map).ToList(),
        };
    }

    private Models.AlignedWordPair Map(V1.AlignedWordPair source)
    {
        return new Models.AlignedWordPair
        {
            SourceIndex = source.SourceIndex,
            TargetIndex = source.TargetIndex,
            Score = source.Score
        };
    }

    private V1.ParallelCorpus Map(
        Shared.Models.ParallelCorpus source,
        TrainingCorpus? trainingCorpus,
        WordAlignmentCorpus? wordAlignmentCorpus,
        bool trainOnAllCorpora,
        bool wordAlignOnAllCorpora
    )
    {
        string? referenceFileLocation =
            source.TargetCorpora.Count > 0 && source.TargetCorpora[0].Files.Count > 0
                ? Map(source.TargetCorpora[0].Files[0]).Location
                : null;

        bool trainOnAllSources =
            trainOnAllCorpora || (trainingCorpus is not null && trainingCorpus.SourceFilters is null);
        bool wordAlignAllSources =
            wordAlignOnAllCorpora || (wordAlignmentCorpus is not null && wordAlignmentCorpus.SourceFilters is null);

        bool trainOnAllTargets =
            trainOnAllCorpora || (trainingCorpus is not null && trainingCorpus.TargetFilters is null);
        bool wordAlignAllTargets =
            wordAlignOnAllCorpora || (wordAlignmentCorpus is not null && wordAlignmentCorpus.TargetFilters is null);

        return new V1.ParallelCorpus
        {
            Id = source.Id,
            SourceCorpora =
            {
                source.SourceCorpora.Select(sc =>
                    Map(
                        sc,
                        trainingCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                        wordAlignmentCorpus?.SourceFilters?.Where(sf => sf.CorpusRef == sc.Id).FirstOrDefault(),
                        referenceFileLocation,
                        trainOnAllSources,
                        wordAlignAllSources
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
                        wordAlignAllTargets
                    )
                )
            }
        };
    }

    private V1.MonolingualCorpus Map(
        Shared.Models.MonolingualCorpus inputCorpus,
        ParallelCorpusFilter? trainingFilter,
        ParallelCorpusFilter? wordAlignmentFilter,
        string? referenceFileLocation,
        bool trainOnAll,
        bool wordAlignOnAll
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

        Dictionary<string, ScriptureChapters>? wordAlignmentChapters = null;
        if (
            wordAlignmentFilter is not null
            && wordAlignmentFilter.ScriptureRange is not null
            && referenceFileLocation is not null
        )
        {
            wordAlignmentChapters = GetChapters(referenceFileLocation, wordAlignmentFilter.ScriptureRange)
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
            wordAlignmentFilter is not null
            && wordAlignmentFilter.TextIds is not null
            && wordAlignmentFilter.ScriptureRange is not null
        )
        {
            throw new InvalidOperationException(
                "Cannot specify both TextIds and ScriptureRange in the word alignment filter."
            );
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
            returnCorpus.WordAlignOnAll = true;
        }
        else
        {
            if (wordAlignmentChapters is not null)
                returnCorpus.WordAlignOnChapters.Add(wordAlignmentChapters);
            if (wordAlignmentFilter?.TextIds is not null)
                returnCorpus.WordAlignOnTextIds.Add(wordAlignmentFilter.TextIds);
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
