using MassTransit.Mediator;
using Serval.Engine.V1;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

public class TranslationEngineService(
    IRepository<TranslationEngine> engines,
    IRepository<TranslationBuild> builds,
    IRepository<Pretranslation> pretranslations,
    IScopedMediator mediator,
    GrpcClientFactory grpcClientFactory,
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory,
    IScriptureDataFileService scriptureDataFileService
)
    : EngineServiceBase<TranslationEngine, TranslationBuild>(
        engines,
        builds,
        grpcClientFactory,
        dataAccessContext,
        loggerFactory
    ),
        ITranslationEngineService
{
    private readonly IRepository<Pretranslation> _pretranslations = pretranslations;
    private readonly IScopedMediator _mediator = mediator;
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;
    private readonly IScriptureDataFileService _scriptureDataFileService = scriptureDataFileService;

    public async Task<Models.TranslationResult> TranslateAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        TranslationEngine engine = await GetAsync(engineId, cancellationToken);
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
        TranslationEngine engine = await GetAsync(engineId, cancellationToken);

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
        TranslationEngine engine = await GetAsync(engineId, cancellationToken);

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
        TranslationEngine engine = await GetAsync(engineId, cancellationToken);

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

    public override async Task<TranslationEngine> CreateAsync(
        TranslationEngine engine,
        CancellationToken cancellationToken = default
    )
    {
        bool updateIsModelPersisted = engine.IsModelPersisted is null;
        CreateEngineParameters parameters = new();
        if (engine.IsModelPersisted is not null)
            parameters.IsModelPersisted = engine.IsModelPersisted.Value;

        string results_serialized = await base.CreateAsync(
            engine,
            JsonSerializer.Serialize(parameters),
            cancellationToken
        );
        CreateEngineResults results = JsonSerializer.Deserialize<CreateEngineResults>(results_serialized)!;
        // IsModelPersisted may be updated by the engine with the respective default.
        engine = engine with
        {
            IsModelPersisted = results.IsModelPersisted
        };
        if (updateIsModelPersisted)
        {
            await Entities.UpdateAsync(
                engine,
                u => u.Set(e => e.IsModelPersisted, results.IsModelPersisted),
                cancellationToken: cancellationToken
            );
        }
        return engine;
    }

    public async Task StartBuildAsync(TranslationBuild build, CancellationToken cancellationToken = default)
    {
        TranslationEngine? engine = await Entities.GetAsync(build.EngineRef, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{build.EngineRef}'.");

        var pretranslate = build.Pretranslate?.ToDictionary(c => c.CorpusRef);
        var trainOn = build.TrainOn?.ToDictionary(c => c.CorpusRef);
        IEnumerable<TranslationCorpus> corpora = engine.Corpora.Select(c =>
        {
            TranslationCorpus corpus = Map(c);
            if (pretranslate?.TryGetValue(c.Id, out PretranslateCorpus? pretranslateCorpus) ?? false)
            {
                corpus.PretranslateAll =
                    pretranslateCorpus.TextIds is null && pretranslateCorpus.ScriptureRange is null;
                if (pretranslateCorpus.TextIds is not null && pretranslateCorpus.ScriptureRange is not null)
                {
                    throw new InvalidOperationException(
                        $"The corpus {c.Id} cannot specify both 'textIds' and 'scriptureRange' for 'pretranslate'."
                    );
                }
                if (pretranslateCorpus.TextIds is not null)
                    corpus.PretranslateTextIds.Add(pretranslateCorpus.TextIds);
                if (!string.IsNullOrEmpty(pretranslateCorpus.ScriptureRange))
                {
                    if (c.TargetFiles.Count > 1 || c.TargetFiles[0].Format != Shared.Contracts.FileFormat.Paratext)
                    {
                        throw new InvalidOperationException(
                            $"The corpus {c.Id} is not compatible with using a scripture range"
                        );
                    }
                    corpus.PretranslateChapters.Add(
                        _scriptureDataFileService
                            .GetChapters(corpus.TargetFiles, pretranslateCorpus.ScriptureRange)
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
            if (trainOn?.TryGetValue(c.Id, out FilteredCorpus? filteredCorpus) ?? false)
            {
                corpus.TrainOnAll = filteredCorpus.TextIds is null && filteredCorpus.ScriptureRange is null;
                if (filteredCorpus.TextIds is not null && filteredCorpus.ScriptureRange is not null)
                {
                    throw new InvalidOperationException(
                        $"The corpus {c.Id} cannot specify both 'textIds' and 'scriptureRange' for trainOn"
                    );
                }
                if (filteredCorpus.TextIds is not null)
                    corpus.TrainOnTextIds.Add(filteredCorpus.TextIds);
                if (!string.IsNullOrEmpty(filteredCorpus.ScriptureRange))
                {
                    if (c.TargetFiles.Count > 1 || c.TargetFiles[0].Format != Shared.Contracts.FileFormat.Paratext)
                    {
                        throw new InvalidOperationException(
                            $"The corpus {c.Id} is not compatible with using a scripture range"
                        );
                    }
                    corpus.TrainOnChapters.Add(
                        _scriptureDataFileService
                            .GetChapters(corpus.TargetFiles, filteredCorpus.ScriptureRange)
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
        });
        await StartBuildAsync(build, JsonSerializer.Serialize(corpora), build.Options, cancellationToken);
    }

    public async Task<ModelDownloadUrl> GetModelDownloadUrlAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        TranslationEngine? engine = await GetAsync(engineId, cancellationToken);
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

    public Task AddCorpusAsync(string engineId, TrainingCorpus corpus, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAsync(engineId, u => u.Add(e => e.Corpora, corpus), cancellationToken: cancellationToken);
    }

    public async Task<TrainingCorpus> UpdateCorpusAsync(
        string engineId,
        string corpusId,
        IReadOnlyList<Shared.Models.CorpusFile>? sourceFiles,
        IReadOnlyList<Shared.Models.CorpusFile>? targetFiles,
        CancellationToken cancellationToken = default
    )
    {
        TranslationEngine? engine = await Entities.UpdateAsync(
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
        TranslationEngine? originalEngine = null;
        await DataAccessContext.WithTransactionAsync(
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

    public Task RemoveDataFileFromAllCorporaAsync(string dataFileId, CancellationToken cancellationToken = default)
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

    private Shared.Models.AlignedWordPair Map(Engine.V1.AlignedWordPair source)
    {
        return new Shared.Models.AlignedWordPair { SourceIndex = source.SourceIndex, TargetIndex = source.TargetIndex };
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

    private TranslationCorpus Map(TrainingCorpus source)
    {
        return new TranslationCorpus
        {
            Id = source.Id,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = { source.SourceFiles.Select(Map) },
            TargetFiles = { source.TargetFiles.Select(Map) }
        };
    }

    private Engine.V1.CorpusFile Map(Shared.Models.CorpusFile source)
    {
        return new Engine.V1.CorpusFile
        {
            TextId = source.TextId,
            Format = (Engine.V1.FileFormat)source.Format,
            Location = Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, source.Filename)
        };
    }
}
