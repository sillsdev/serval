using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Services;

public class StatisticalEngineService(
    IDistributedReaderWriterLockFactory lockFactory,
    [FromKeyedServices(EngineGroup.WordAlignment)] IPlatformService platformService,
    IDataAccessContext dataAccessContext,
    IRepository<WordAlignmentEngine> engines,
    StatisticalEngineStateService stateService,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    IClearMLQueueService clearMLQueueService
) : IWordAlignmentEngineService
{
    private readonly IDistributedReaderWriterLockFactory _lockFactory = lockFactory;
    private readonly IPlatformService _platformService = platformService;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<WordAlignmentEngine> _engines = engines;
    private readonly StatisticalEngineStateService _stateService = stateService;
    private readonly IBuildJobService<WordAlignmentEngine> _buildJobService = buildJobService;
    private readonly IClearMLQueueService _clearMLQueueService = clearMLQueueService;

    public EngineType Type => EngineType.Statistical;

    public async Task CreateAsync(
        string engineId,
        string? engineName,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var waEngine = new WordAlignmentEngine
            {
                EngineId = engineId,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Type = EngineType.Statistical,
            };
            await _engines.InsertAsync(waEngine, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // this method is idempotent, so ignore if the engine already exists
        }

        StatisticalEngineState state = _stateService.Get(engineId);
        state.InitNew();
    }

    public async Task<WordAlignmentResult> AlignAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        CancellationToken cancellationToken = default
    )
    {
        WordAlignmentEngine engine = await GetBuiltEngineAsync(engineId, cancellationToken);
        StatisticalEngineState state = _stateService.Get(engineId);
        if (state.IsMarkedForDeletion)
            throw new InvalidOperationException("Engine is marked for deletion.");

        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId, cancellationToken);
        WordAlignmentResult result = await @lock.ReaderLockAsync(
            async ct =>
            {
                IWordAlignmentModel wordAlignmentModel = await state.GetEngineAsync(engine.BuildRevision, ct);
                LatinWordTokenizer tokenizer = new();

                // there is no way to cancel this call
                IReadOnlyList<string> sourceTokens = tokenizer.Tokenize(sourceSegment).ToList();
                IReadOnlyList<string> targetTokens = tokenizer.Tokenize(targetSegment).ToList();
                IReadOnlyCollection<SIL.Machine.Corpora.AlignedWordPair> wordPairs =
                    wordAlignmentModel.GetBestAlignedWordPairs(sourceTokens, targetTokens);
                return new WordAlignmentResult()
                {
                    SourceTokens = { sourceTokens },
                    TargetTokens = { targetTokens },
                    Alignment = { wordPairs.Select(Map) }
                };
            },
            cancellationToken: cancellationToken
        );

        state.Touch();
        return result;
    }

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        // there is no way to cancel this call
        StatisticalEngineState state = _stateService.Get(engineId);
        state.IsMarkedForDeletion = true;

        await CancelBuildJobAsync(engineId, cancellationToken);

        await _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                await _engines.DeleteAsync(e => e.EngineId == engineId, ct);
            },
            cancellationToken: CancellationToken.None
        );
        await _buildJobService.DeleteEngineAsync(engineId, CancellationToken.None);

        _stateService.Remove(engineId);
        state.DeleteData();
        state.Dispose();
        await _lockFactory.DeleteAsync(engineId, CancellationToken.None);
    }

    public async Task StartBuildAsync(
        string engineId,
        string buildId,
        string? buildOptions,
        IReadOnlyList<SIL.ServiceToolkit.Models.ParallelCorpus> corpora,
        CancellationToken cancellationToken = default
    )
    {
        await _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                bool building = !await _buildJobService.StartBuildJobAsync(
                    BuildJobRunnerType.Hangfire,
                    EngineType.Statistical,
                    engineId,
                    buildId,
                    BuildStage.Preprocess,
                    corpora,
                    buildOptions,
                    ct
                );
                // If there is a pending/running build, then no need to start a new one.
                if (building)
                    await _platformService.BuildCanceledAsync(buildId, ct);
            },
            cancellationToken
        );

        StatisticalEngineState state = _stateService.Get(engineId);
        state.Touch();
    }

    public async Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        string? buildId = await CancelBuildJobAsync(engineId, cancellationToken);
        if (buildId is null)
            return null;

        StatisticalEngineState state = _stateService.Get(engineId);
        state.Touch();
        return buildId;
    }

    public int GetQueueSize()
    {
        return _clearMLQueueService.GetQueueSize(Type);
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

    private async Task<WordAlignmentEngine> GetEngineAsync(string engineId, CancellationToken cancellationToken)
    {
        WordAlignmentEngine? engine = await _engines.GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EngineNotFoundException($"The engine {engineId} does not exist.");
        return engine;
    }

    private async Task<WordAlignmentEngine> GetBuiltEngineAsync(string engineId, CancellationToken cancellationToken)
    {
        WordAlignmentEngine engine = await GetEngineAsync(engineId, cancellationToken);
        if (engine.BuildRevision == 0)
            throw new EngineNotBuiltException("The engine must be built first.");
        return engine;
    }

    private static WordAlignment.V1.AlignedWordPair Map(SIL.Machine.Corpora.AlignedWordPair alignedWordPair)
    {
        return new WordAlignment.V1.AlignedWordPair
        {
            SourceIndex = alignedWordPair.SourceIndex,
            TargetIndex = alignedWordPair.TargetIndex,
            Score = alignedWordPair.TranslationScore
        };
    }
}
