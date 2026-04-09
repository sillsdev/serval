using Serval.Shared.Contracts;
using Serval.Translation.Contracts;

namespace Serval.Machine.Shared.Services;

public class SmtTransferEngineService(
    IDistributedReaderWriterLockFactory lockFactory,
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IRepository<TrainSegmentPair> trainSegmentPairs,
    SmtTransferEngineStateService stateService,
    IBuildJobService<TranslationEngine> buildJobService,
    IClearMLQueueService clearMLQueueService
) : ITranslationEngineService
{
    private readonly IDistributedReaderWriterLockFactory _lockFactory = lockFactory;
    private readonly IPlatformService _platformService = platformService;
    private readonly IRepository<TranslationEngine> _engines = engines;
    private readonly IRepository<TrainSegmentPair> _trainSegmentPairs = trainSegmentPairs;
    private readonly SmtTransferEngineStateService _stateService = stateService;
    private readonly IBuildJobService<TranslationEngine> _buildJobService = buildJobService;
    private readonly IClearMLQueueService _clearMLQueueService = clearMLQueueService;

    public async Task CreateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        string? engineName = null,
        bool? isModelPersisted = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var translationEngine = new TranslationEngine
            {
                EngineId = engineId,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Type = EngineType.SmtTransfer,
                IsModelPersisted = isModelPersisted ?? true, // models are persisted if not specified
            };
            await _engines.InsertAsync(translationEngine, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // this method is idempotent, so ignore if the engine already exists
        }

        SmtTransferEngineState state = _stateService.Get(engineId);
        state.InitNew();
    }

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        SmtTransferEngineState state = _stateService.Get(engineId);
        state.IsMarkedForDeletion = true;

        await CancelBuildJobAsync(engineId, cancellationToken);
        await _engines.DeleteAsync(e => e.EngineId == engineId, cancellationToken);
        await _trainSegmentPairs.DeleteAllAsync(p => p.TranslationEngineRef == engineId, cancellationToken);
        await _buildJobService.DeleteEngineAsync(engineId, cancellationToken);

        // after this point, we cannot cancel
        _stateService.Remove(engineId);
        state.DeleteData();
        state.Dispose();
        await _lockFactory.DeleteAsync(engineId, CancellationToken.None);
    }

    public async Task UpdateAsync(
        string engineId,
        string? sourceLanguage,
        string? targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        await _engines.UpdateAsync(
            e => e.EngineId == engineId,
            u =>
            {
                if (sourceLanguage is not null)
                    u.Set(e => e.SourceLanguage, sourceLanguage);
                if (targetLanguage is not null)
                    u.Set(e => e.TargetLanguage, targetLanguage);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyList<TranslationResultContract>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        TranslationEngine engine = await GetBuiltEngineAsync(engineId, cancellationToken);
        SmtTransferEngineState state = _stateService.Get(engineId);
        if (state.IsMarkedForDeletion)
            throw new EngineNotFoundException($"The engine {engineId} is marked for deletion.");

        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId, cancellationToken);
        IReadOnlyList<TranslationResult> results = await @lock.ReaderLockAsync(
            async ct =>
            {
                HybridTranslationEngine hybridEngine = await state.GetHybridEngineAsync(engine.BuildRevision, ct);
                // there is no way to cancel this call
                return hybridEngine.Translate(n, segment);
            },
            cancellationToken: cancellationToken
        );

        state.Touch();
        return results.Select(Map).ToList();
    }

    public async Task<WordGraphContract> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        TranslationEngine engine = await GetBuiltEngineAsync(engineId, cancellationToken);
        SmtTransferEngineState state = _stateService.Get(engineId);
        if (state.IsMarkedForDeletion)
            throw new EngineNotFoundException($"The engine {engineId} is marked for deletion.");

        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId, cancellationToken);
        WordGraph result = await @lock.ReaderLockAsync(
            async ct =>
            {
                HybridTranslationEngine hybridEngine = await state.GetHybridEngineAsync(engine.BuildRevision, ct);
                // there is no way to cancel this call
                return hybridEngine.GetWordGraph(segment);
            },
            cancellationToken: cancellationToken
        );

        state.Touch();
        return Map(result);
    }

    public async Task TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    )
    {
        SmtTransferEngineState state = _stateService.Get(engineId);
        if (state.IsMarkedForDeletion)
            throw new EngineNotFoundException($"The engine {engineId} is marked for deletion.");

        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId, cancellationToken);
        await @lock.WriterLockAsync(
            async ct =>
            {
                TranslationEngine engine = await GetEngineAsync(engineId, ct);

                HybridTranslationEngine hybridEngine = await state.GetHybridEngineAsync(engine.BuildRevision, ct);
                // there is no way to cancel this call
                hybridEngine.TrainSegment(sourceSegment, targetSegment, sentenceStart);

                if (engine.CollectTrainSegmentPairs ?? false)
                {
                    await _trainSegmentPairs.InsertAsync(
                        new TrainSegmentPair
                        {
                            TranslationEngineRef = engineId,
                            Source = sourceSegment,
                            Target = targetSegment,
                            SentenceStart = sentenceStart,
                        },
                        CancellationToken.None
                    );
                }

                state.IsUpdated = true;
            },
            cancellationToken: cancellationToken
        );

        await _platformService.IncrementTrainSizeAsync(engineId, cancellationToken: CancellationToken.None);
        state.Touch();
    }

    public async Task StartBuildAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> corpora,
        string? options = null,
        CancellationToken cancellationToken = default
    )
    {
        bool building = !await _buildJobService.StartBuildJobAsync(
            BuildJobRunnerType.Hangfire,
            EngineType.SmtTransfer,
            engineId,
            buildId,
            BuildStage.Preprocess,
            corpora,
            options,
            cancellationToken
        );
        // If there is a pending/running build, then no need to start a new one.
        if (building)
            await _platformService.BuildCanceledAsync(buildId, CancellationToken.None);

        SmtTransferEngineState state = _stateService.Get(engineId);
        state.Touch();
    }

    public async Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        string? buildId = await CancelBuildJobAsync(engineId, cancellationToken);
        if (buildId is null)
            return null;

        SmtTransferEngineState state = _stateService.Get(engineId);
        state.Touch();
        return buildId;
    }

    public Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_clearMLQueueService.GetQueueSize(EngineType.SmtTransfer));
    }

    public Task<LanguageInfoContract> GetLanguageInfoAsync(
        string language,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(new LanguageInfoContract { IsNative = true, InternalCode = language });
    }

    private async Task<string?> CancelBuildJobAsync(string engineId, CancellationToken cancellationToken)
    {
        (string? buildId, BuildJobState jobState) = await _buildJobService.CancelBuildJobAsync(
            engineId,
            cancellationToken
        );
        if (buildId is not null && jobState is BuildJobState.None)
            await _platformService.BuildCanceledAsync(buildId, CancellationToken.None);
        return buildId;
    }

    public Task<ModelDownloadUrlContract> GetModelDownloadUrlAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    private async Task<TranslationEngine> GetEngineAsync(string engineId, CancellationToken cancellationToken)
    {
        TranslationEngine? engine = await _engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
        if (engine is null)
            throw new EngineNotFoundException($"The engine {engineId} does not exist.");
        return engine;
    }

    private async Task<TranslationEngine> GetBuiltEngineAsync(string engineId, CancellationToken cancellationToken)
    {
        TranslationEngine engine = await GetEngineAsync(engineId, cancellationToken);
        if (engine.BuildRevision == 0)
            throw new EngineNotBuiltException($"The engine {engineId} must be built first.");
        return engine;
    }

    private static TranslationResultContract Map(TranslationResult source)
    {
        return new TranslationResultContract
        {
            Translation = source.Translation,
            SourceTokens = source.SourceTokens.ToArray(),
            TargetTokens = source.TargetTokens.ToArray(),
            Confidences = source.Confidences.ToArray(),
            Sources = source.Sources.Select(Map).ToList(),
            Alignment = MapAlignment(source.Alignment).ToList(),
            Phrases = source.Phrases.Select(Map).ToList(),
        };
    }

    private static WordGraphContract Map(WordGraph source)
    {
        return new WordGraphContract
        {
            SourceTokens = source.SourceTokens.ToArray(),
            InitialStateScore = source.InitialStateScore,
            FinalStates = source.FinalStates.ToHashSet(),
            Arcs = source.Arcs.Select(Map).ToList(),
        };
    }

    private static WordGraphArcContract Map(WordGraphArc source)
    {
        return new WordGraphArcContract
        {
            PrevState = source.PrevState,
            NextState = source.NextState,
            Score = source.Score,
            TargetTokens = source.TargetTokens.ToArray(),
            Confidences = source.Confidences.ToArray(),
            SourceSegmentStart = source.SourceSegmentRange.Start,
            SourceSegmentEnd = source.SourceSegmentRange.End,
            Sources = source.Sources.Select(Map).ToList(),
            Alignment = MapAlignment(source.Alignment).ToList(),
        };
    }

    private static IReadOnlySet<TranslationSource> Map(TranslationSources source)
    {
        return Enum.GetValues<TranslationSources>()
            .Where(s => s != TranslationSources.None && source.HasFlag(s))
            .Select(s =>
                s switch
                {
                    TranslationSources.Smt => TranslationSource.Primary,
                    TranslationSources.Nmt => TranslationSource.Primary,
                    TranslationSources.Transfer => TranslationSource.Secondary,
                    TranslationSources.Prefix => TranslationSource.Human,
                    _ => TranslationSource.Primary,
                }
            )
            .ToHashSet();
    }

    private static IEnumerable<AlignedWordPairContract> MapAlignment(WordAlignmentMatrix source)
    {
        for (int i = 0; i < source.RowCount; i++)
        {
            for (int j = 0; j < source.ColumnCount; j++)
            {
                if (source[i, j])
                {
                    yield return new AlignedWordPairContract { SourceIndex = i, TargetIndex = j };
                }
            }
        }
    }

    private static PhraseContract Map(Phrase source)
    {
        return new PhraseContract
        {
            SourceSegmentStart = source.SourceSegmentRange.Start,
            SourceSegmentEnd = source.SourceSegmentRange.End,
            TargetSegmentCut = source.TargetSegmentCut,
        };
    }
}
