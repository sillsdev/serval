namespace Serval.Machine.Shared.Services;

public class SmtTransferEngineService(
    IDistributedReaderWriterLockFactory lockFactory,
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IDataAccessContext dataAccessContext,
    IRepository<TranslationEngine> engines,
    IRepository<TrainSegmentPair> trainSegmentPairs,
    SmtTransferEngineStateService stateService,
    IBuildJobService<TranslationEngine> buildJobService,
    IClearMLQueueService clearMLQueueService
) : ITranslationEngineService
{
    private readonly IDistributedReaderWriterLockFactory _lockFactory = lockFactory;
    private readonly IPlatformService _platformService = platformService;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<TranslationEngine> _engines = engines;
    private readonly IRepository<TrainSegmentPair> _trainSegmentPairs = trainSegmentPairs;
    private readonly SmtTransferEngineStateService _stateService = stateService;
    private readonly IBuildJobService<TranslationEngine> _buildJobService = buildJobService;
    private readonly IClearMLQueueService _clearMLQueueService = clearMLQueueService;

    public EngineType Type => EngineType.SmtTransfer;

    public async Task<TranslationEngine> CreateAsync(
        string engineId,
        string? engineName,
        string sourceLanguage,
        string targetLanguage,
        bool? isModelPersisted = null,
        CancellationToken cancellationToken = default
    )
    {
        if (isModelPersisted == false)
        {
            throw new NotSupportedException(
                "SMT transfer engines do not support non-persisted models."
                    + "Please remove the isModelPersisted parameter or set it to true."
            );
        }

        TranslationEngine translationEngine = await _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                var translationEngine = new TranslationEngine
                {
                    EngineId = engineId,
                    SourceLanguage = sourceLanguage,
                    TargetLanguage = targetLanguage,
                    Type = EngineType.SmtTransfer,
                    IsModelPersisted = isModelPersisted ?? true // models are persisted if not specified
                };
                await _engines.InsertAsync(translationEngine, ct);
                await _buildJobService.CreateEngineAsync(engineId, engineName, ct);
                return translationEngine;
            },
            cancellationToken: cancellationToken
        );

        SmtTransferEngineState state = _stateService.Get(engineId);
        state.InitNew();
        return translationEngine;
    }

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        // there is no way to cancel this call
        SmtTransferEngineState state = _stateService.Get(engineId);
        state.IsMarkedForDeletion = true;

        await CancelBuildJobAsync(engineId, CancellationToken.None);

        await _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                await _engines.DeleteAsync(e => e.EngineId == engineId, ct);
                await _trainSegmentPairs.DeleteAllAsync(p => p.TranslationEngineRef == engineId, ct);
            },
            cancellationToken: CancellationToken.None
        );
        await _buildJobService.DeleteEngineAsync(engineId, CancellationToken.None);
        _stateService.Remove(engineId);
        state.DeleteData();
        state.Dispose();
        await _lockFactory.DeleteAsync(engineId, CancellationToken.None);
    }

    public async Task UpdateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        await _engines.UpdateAsync(
            e => e.EngineId == engineId,
            u =>
            {
                u.Set(e => e.SourceLanguage, sourceLanguage);
                u.Set(e => e.TargetLanguage, targetLanguage);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyList<TranslationResult>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        TranslationEngine engine = await GetBuiltEngineAsync(engineId, cancellationToken);
        SmtTransferEngineState state = _stateService.Get(engineId);
        if (state.IsMarkedForDeletion)
            throw new InvalidOperationException("Engine is marked for deletion.");

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
        return results;
    }

    public async Task<WordGraph> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        TranslationEngine engine = await GetBuiltEngineAsync(engineId, cancellationToken);
        SmtTransferEngineState state = _stateService.Get(engineId);
        if (state.IsMarkedForDeletion)
            throw new InvalidOperationException("Engine is marked for deletion.");

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
        return result;
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
            throw new InvalidOperationException("Engine is marked for deletion.");

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
                            SentenceStart = sentenceStart
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
        string? buildOptions,
        IReadOnlyList<ParallelCorpus> corpora,
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
            buildOptions,
            cancellationToken
        );
        // If there is a pending/running build, then no need to start a new one.
        if (building)
            throw new InvalidOperationException("The engine is already building or in the process of canceling.");

        SmtTransferEngineState state = _stateService.Get(engineId);
        state.Touch();
    }

    public async Task<string> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        string? buildId = await CancelBuildJobAsync(engineId, cancellationToken);
        if (buildId is null)
            throw new InvalidOperationException("The engine is not currently building.");

        SmtTransferEngineState state = _stateService.Get(engineId);
        state.Touch();
        return buildId;
    }

    public int GetQueueSize()
    {
        return _clearMLQueueService.GetQueueSize(Type);
    }

    public bool IsLanguageNativeToModel(string language, out string internalCode)
    {
        throw new NotSupportedException("SMT transfer engines do not support language info.");
    }

    private async Task<string?> CancelBuildJobAsync(string engineId, CancellationToken cancellationToken)
    {
        string? buildId = null;
        await _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                (buildId, BuildJobState jobState) = await _buildJobService.CancelBuildJobAsync(engineId, ct);
                if (buildId is not null && jobState is BuildJobState.None)
                    await _platformService.BuildCanceledAsync(buildId, CancellationToken.None);
            },
            cancellationToken: cancellationToken
        );
        return buildId;
    }

    public Task<ModelDownloadUrl> GetModelDownloadUrlAsync(
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
            throw new InvalidOperationException($"The engine {engineId} does not exist.");
        return engine;
    }

    private async Task<TranslationEngine> GetBuiltEngineAsync(string engineId, CancellationToken cancellationToken)
    {
        TranslationEngine engine = await GetEngineAsync(engineId, cancellationToken);
        if (engine.BuildRevision == 0)
            throw new EngineNotBuiltException("The engine must be built first.");
        return engine;
    }
}
