using MassTransit.Mediator;
using Serval.Translation.V1;
using SIL.Scripture;

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
                _scriptureDataFileService.GetParatextProjectSettings(fileLocation).Versification //TODO corpus.TargetFiles.First().Location
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
                var pretranslate = build.Pretranslate?.ToDictionary(c => c.ParallelCorpusRef!);
                List<TrainingSubcorpus>? corporaPerTexts = GetTrainingCorporaPerTexts(
                    build.TrainOn ?? new List<TrainingCorpus>(),
                    engine.ParallelCorpora
                );
                request = new StartBuildRequest
                {
                    EngineType = engine.Type,
                    EngineId = engine.Id,
                    BuildId = build.Id,
                    Corpora =
                    {
                        engine.ParallelCorpora.SelectMany(c =>
                            Map(
                                c,
                                corporaPerTexts,
                                pretranslate?.GetValueOrDefault(c.Id),
                                trainOnAll: build.TrainOn == null || corporaPerTexts == null
                            )
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

    private record TrainingSubcorpus
    {
        public Dictionary<string, List<int>>? Chapters { get; init; }
        public List<string>? TextIds { get; init; }
        public required List<string> SourceCorporaRefs { get; init; }
        public required List<string> TargetCorporaRefs { get; init; }
    }

    private enum CorpusType
    {
        Source = 0,
        Target = 1
    }

    private List<TrainingSubcorpus>? GetTrainingCorporaPerTexts(
        IReadOnlyList<TrainingCorpus> trainingCorpora,
        IReadOnlyList<ParallelCorpus> parallelCorpora
    )
    {
        Dictionary<string, List<(string Location, Shared.Contracts.FileFormat Format)>> fileLocations = parallelCorpora
            .SelectMany(pc => pc.SourceCorpora.Concat(pc.TargetCorpora))
            .Where(c => c.Files.Count > 0)
            .Select(c =>
                (
                    c.Id,
                    c.Files.Select(f =>
                        (Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, f.Filename), f.Format)
                    )
                        .ToList()
                )
            )
            .ToDictionary();

        List<string> parallelCorpusSourceIds = parallelCorpora
            .SelectMany(pc => pc.SourceCorpora)
            .Select(sc => sc.Id)
            .ToList();

        List<string> parallelCorpusTargetIds = parallelCorpora
            .SelectMany(pc => pc.TargetCorpora)
            .Select(tc => tc.Id)
            .ToList();

        List<string> trainingCorpusSourceFilterCorpusIds = trainingCorpora
            .Where(tc => tc.SourceFilters != null)
            .SelectMany(tc => tc.SourceFilters!)
            .Select(sf => sf.CorpusRef)
            .ToList();

        List<string> trainingCorpusTargetFilterCorpusIds = trainingCorpora
            .Where(tc => tc.TargetFilters != null)
            .SelectMany(tc => tc.TargetFilters!)
            .Select(sf => sf.CorpusRef)
            .ToList();

        if (trainingCorpusSourceFilterCorpusIds.Where(id => !parallelCorpusSourceIds.Contains(id)).Any())
        {
            throw new InvalidOperationException(
                "Corpus specified in source filter could not be found within parallel corpus."
            ); //TODO better error message?
        }
        if (trainingCorpusTargetFilterCorpusIds.Where(id => !parallelCorpusTargetIds.Contains(id)).Any())
        {
            throw new InvalidOperationException(
                "Corpus specified in target filter could not be found within parallel corpus."
            ); //TODO better error message?
        }

        List<string> unfilteredSourceCorpora = trainingCorpora
            .Where(tc => tc.SourceFilters == null)
            .Select(tc => tc.ParallelCorpusRef)
            .SelectMany(r => parallelCorpora.Where(pc => pc.Id == r).First().SourceCorpora.Select(c => c.Id))
            .ToList();

        List<string> unfilteredTargetCorpora = trainingCorpora
            .Where(tc => tc.TargetFilters == null)
            .Select(tc => tc.ParallelCorpusRef)
            .SelectMany(r => parallelCorpora.Where(pc => pc.Id == r).First().TargetCorpora.Select(c => c.Id))
            .ToList();

        IEnumerable<(
            (string Book, int Chapter) Verse,
            (string Ref, CorpusType Type, Shared.Contracts.FileFormat Format) Corpus
        )> ProcessFilter(ParallelCorpusFilter filter, CorpusType corpusType)
        {
            var bookChapters = new List<(string, int)>();
            if (
                fileLocations.TryGetValue(
                    filter.CorpusRef,
                    out List<(string Location, Shared.Contracts.FileFormat Format)>? files
                )
            )
            {
                if (files.Count == 1 && files[0].Format == Shared.Contracts.FileFormat.Paratext)
                {
                    ScrVers versification = _scriptureDataFileService
                        .GetParatextProjectSettings(files[0].Location)
                        .Versification;
                    if (filter.TextIds != null)
                    {
                        bookChapters = filter
                            .TextIds.SelectMany(id =>
                                Enumerable
                                    .Range(1, versification.GetLastChapter(Canon.BookIdToNumber(id)))
                                    .Select(chpt => (id, chpt))
                            )
                            .ToList();
                    }
                    else if (!string.IsNullOrEmpty(filter.ScriptureRange))
                    {
                        bookChapters = GetChapters(files[0].Location, filter.ScriptureRange)
                            .Select(kvp =>
                                (
                                    kvp.Key,
                                    kvp.Value.Count == 0
                                        ? Enumerable.Range(
                                            1,
                                            versification.GetLastChapter(Canon.BookIdToNumber(kvp.Key))
                                        )
                                        : kvp.Value
                                )
                            )
                            .SelectMany(tup => tup.Item2.Select(chpt => (tup.Item1, chpt)))
                            .ToList();
                    }
                    return bookChapters.Select(bc =>
                        ((bc.Item1, bc.Item2), (filter.CorpusRef, corpusType, Shared.Contracts.FileFormat.Paratext))
                    );
                }
                else
                {
                    if (!string.IsNullOrEmpty(filter.ScriptureRange))
                    {
                        throw new InvalidOperationException(
                            $"The corpus {filter.CorpusRef} is not compatible with using a scripture range"
                        );
                    }
                    if (filter.TextIds != null)
                    {
                        return filter.TextIds.Select(tid =>
                            ((tid, 0), (filter.CorpusRef, corpusType, Shared.Contracts.FileFormat.Text))
                        );
                    }
                }
            }
            throw new InvalidOperationException($"Could not locate files associated with {filter.CorpusRef}");
        }

        IReadOnlyList<(
            (string Book, int Chapter) Verse,
            (string Ref, CorpusType Type, Shared.Contracts.FileFormat Format) Corpus
        )> sourceCorporaPerChapters = trainingCorpora
            .Where(tc => tc.SourceFilters != null)
            .SelectMany(tc => tc.SourceFilters!)
            .SelectMany(sf => ProcessFilter(sf, CorpusType.Source))
            .ToList();

        IReadOnlyList<(
            (string Book, int Chapter) Verse,
            (string CorpusRef, CorpusType CorpusType, Shared.Contracts.FileFormat Format) Corpus
        )> targetCorporaPerChapters = trainingCorpora
            .Where(tc => tc.TargetFilters != null)
            .SelectMany(tc => tc.TargetFilters!)
            .SelectMany(tf => ProcessFilter(tf, CorpusType.Target))
            .ToList();

        List<TrainingSubcorpus> trainingSubcorpora = sourceCorporaPerChapters
            .Concat(targetCorporaPerChapters)
            .Aggregate(
                new Dictionary<
                    (string Book, int Chapter),
                    List<(string CorpusRef, CorpusType CorpusType, Shared.Contracts.FileFormat CorpusFormat)>
                >(),
                (dict, tup) =>
                {
                    if (
                        dict.TryGetValue(
                            tup.Verse,
                            out List<(string, CorpusType, Shared.Contracts.FileFormat)>? corporaList
                        )
                    )
                    {
                        corporaList.Add(tup.Corpus);
                    }
                    else
                    {
                        dict[tup.Verse] = new() { tup.Corpus };
                    }
                    return dict;
                }
            )
            .Aggregate(
                new Dictionary<
                    List<(string CorpusRef, CorpusType CorpusType, Shared.Contracts.FileFormat CorpusFormat)>,
                    Dictionary<string, List<int>>
                >(new CorpusListValueComparer()),
                (dict, kvp) =>
                {
                    if (dict.TryGetValue(kvp.Value, out Dictionary<string, List<int>>? chaptersPerBook))
                    {
                        if (chaptersPerBook.TryGetValue(kvp.Key.Book, out List<int>? chapters))
                        {
                            if (!chapters.Contains(kvp.Key.Chapter))
                                chapters.Add(kvp.Key.Chapter);
                        }
                        else
                        {
                            chaptersPerBook[kvp.Key.Book] = new List<int>() { kvp.Key.Chapter };
                        }
                    }
                    else
                    {
                        dict[kvp.Value] = new Dictionary<string, List<int>>()
                        {
                            {
                                kvp.Key.Book,
                                new() { kvp.Key.Chapter }
                            }
                        };
                    }
                    return dict;
                }
            )
            .Select(kvp => new TrainingSubcorpus()
            {
                SourceCorporaRefs = kvp.Key.Where(corpus => corpus.CorpusType == CorpusType.Source)
                    .Select(corpus => corpus.CorpusRef)
                    .Concat(unfilteredSourceCorpora)
                    .ToList(),
                TargetCorporaRefs = kvp.Key.Where(corpus => corpus.CorpusType == CorpusType.Target)
                    .Select(corpus => corpus.CorpusRef)
                    .Concat(unfilteredTargetCorpora)
                    .ToList(),
                Chapters = kvp.Key.All(corpus => corpus.CorpusFormat == Shared.Contracts.FileFormat.Paratext)
                    ? kvp.Value
                    : null,
                TextIds = kvp.Key.All(corpus => corpus.CorpusFormat == Shared.Contracts.FileFormat.Text)
                    ? kvp.Value.Select(kvp => kvp.Key).ToList()
                    : null
            })
            .ToList();
        if (trainingSubcorpora.Count == 0)
        {
            return null;
        }
        return trainingSubcorpora;
    }

    private class CorpusListValueComparer
        : IEqualityComparer<List<(string CorpusRef, CorpusType CorpusType, Shared.Contracts.FileFormat CorpusFormat)>>
    {
        public bool Equals(
            List<(string CorpusRef, CorpusType CorpusType, Shared.Contracts.FileFormat CorpusFormat)>? x,
            List<(string CorpusRef, CorpusType CorpusType, Shared.Contracts.FileFormat CorpusFormat)>? y
        )
        {
            if (x == y)
                return true;
            return x != null && y != null && x.SequenceEqual(y);
        }

        public int GetHashCode(
            [DisallowNull] List<(string CorpusRef, CorpusType CorpusType, Shared.Contracts.FileFormat CorpusFormat)> obj
        )
        {
            int hash = 31;
            foreach ((string corpusRef, CorpusType type, Shared.Contracts.FileFormat format) in obj)
            {
                hash = hash * 71 + corpusRef.GetHashCode() + type.GetHashCode() + format.GetHashCode();
            }
            return hash;
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

    public Task AddParallelCorpus(string engineId, ParallelCorpus corpus, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAsync(
            engineId,
            u => u.Add(e => e.ParallelCorpora, corpus),
            cancellationToken: cancellationToken
        );
    }

    public async Task<ParallelCorpus> UpdateParallelCorpusAsync(
        string engineId,
        string parallelCorpusId,
        IReadOnlyList<ParallelCorpusSubcorpus>? sourceCorpora,
        IReadOnlyList<ParallelCorpusSubcorpus>? targetCorpora,
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

    private V1.Corpus Map(Models.Corpus source, TrainingCorpus? trainingCorpus, PretranslateCorpus? pretranslateCorpus)
    {
        var corpus = new V1.Corpus
        {
            Id = source.Id,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = { source.SourceFiles.Select(Map) },
            TargetFiles = { source.TargetFiles.Select(Map) }
        };
        if (pretranslateCorpus != null)
        {
            corpus.PretranslateAll = pretranslateCorpus.TextIds is null && pretranslateCorpus.ScriptureRange is null;
            if (pretranslateCorpus.TextIds is not null && pretranslateCorpus.ScriptureRange is not null)
            {
                throw new InvalidOperationException(
                    $"The corpus {source.Id} cannot specify both 'textIds' and 'scriptureRange' for 'pretranslate'."
                );
            }
            if (pretranslateCorpus.TextIds is not null)
                corpus.PretranslateTextIds.Add(pretranslateCorpus.TextIds);
            if (!string.IsNullOrEmpty(pretranslateCorpus.ScriptureRange))
            {
                if (
                    source.TargetFiles.Count > 1
                    || source.TargetFiles[0].Format != Shared.Contracts.FileFormat.Paratext
                )
                {
                    throw new InvalidOperationException(
                        $"The corpus {source.Id} is not compatible with using a scripture range"
                    );
                }
                corpus.PretranslateChapters.Add(
                    GetChapters(corpus.TargetFiles[0].Location, pretranslateCorpus.ScriptureRange)
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
        if (trainingCorpus != null)
        {
            corpus.TrainOnAll = trainingCorpus.TextIds is null && trainingCorpus.ScriptureRange is null;
            if (trainingCorpus.TextIds is not null && trainingCorpus.ScriptureRange is not null)
            {
                throw new InvalidOperationException(
                    $"The corpus {source.Id} cannot specify both 'textIds' and 'scriptureRange' for trainOn"
                );
            }
            if (trainingCorpus.TextIds is not null)
                corpus.TrainOnTextIds.Add(trainingCorpus.TextIds);
            if (!string.IsNullOrEmpty(trainingCorpus.ScriptureRange))
            {
                if (
                    source.TargetFiles.Count > 1
                    || source.TargetFiles[0].Format != Shared.Contracts.FileFormat.Paratext
                )
                {
                    throw new InvalidOperationException(
                        $"The corpus {source.Id} is not compatible with using a scripture range"
                    );
                }
                corpus.TrainOnChapters.Add(
                    GetChapters(corpus.TargetFiles[0].Location, trainingCorpus.ScriptureRange)
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
        else
        {
            corpus.TrainOnAll = true;
        }
        return corpus;
    }

    private IEnumerable<V1.Corpus> Map(
        ParallelCorpus source,
        List<TrainingSubcorpus>? trainingCorpora,
        PretranslateCorpus? pretranslateCorpus,
        bool trainOnAll = false
    )
    {
        if (pretranslateCorpus != null)
        {
            var corpus = new V1.Corpus
            {
                Id = source.Id,
                SourceLanguage = source.SourceCorpora[0].Language,
                TargetLanguage = source.TargetCorpora[0].Language,
                SourceFiles = { source.SourceCorpora.SelectMany(c => c.Files.Select(Map)) },
                TargetFiles = { source.TargetCorpora.SelectMany(c => c.Files.Select(Map)) }
            };
            yield return corpus.Clone();
            corpus.PretranslateChapters.Clear();
        }
        if (!trainOnAll && trainingCorpora != null)
        {
            foreach (TrainingSubcorpus trainingCorpus in trainingCorpora)
            {
                var corpus = new V1.Corpus
                {
                    Id = source.Id,
                    SourceLanguage = source.SourceCorpora[0].Language,
                    TargetLanguage = source.TargetCorpora[0].Language,
                    SourceFiles =
                    {
                        source
                            .SourceCorpora.Where(sc => trainingCorpus.SourceCorporaRefs.Contains(sc.Id))
                            .SelectMany(sc => sc.Files)
                            .Select(Map)
                    },
                    TargetFiles =
                    {
                        source
                            .TargetCorpora.Where(sc => trainingCorpus.TargetCorporaRefs.Contains(sc.Id))
                            .SelectMany(sc => sc.Files)
                            .Select(Map)
                    }
                };
                if (trainingCorpus.Chapters != null)
                {
                    corpus.TrainOnChapters.Add(
                        trainingCorpus
                            .Chapters.Select(
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
                if (trainingCorpus.TextIds != null)
                {
                    corpus.TrainOnTextIds.Add(trainingCorpus.TextIds);
                }

                yield return corpus.Clone();
                corpus.TrainOnChapters.Clear();
                corpus.TrainOnTextIds.Clear();
            }
        }
        else
        {
            yield return new V1.Corpus
            {
                Id = source.Id,
                SourceLanguage = source.SourceCorpora[0].Language,
                TargetLanguage = source.TargetCorpora[0].Language,
                SourceFiles = { source.SourceCorpora.SelectMany(c => c.Files.Select(Map)) },
                TargetFiles = { source.TargetCorpora.SelectMany(c => c.Files.Select(Map)) },
                TrainOnAll = trainOnAll
            };
        }
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
