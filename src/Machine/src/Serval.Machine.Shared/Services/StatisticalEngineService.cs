﻿namespace Serval.Machine.Shared.Services;

public class StatisticalEngineService(
    IDistributedReaderWriterLockFactory lockFactory,
    IEnumerable<IPlatformService> platformServices,
    IDataAccessContext dataAccessContext,
    IRepository<WordAlignmentEngine> engines,
    WordAlignmentEngineStateService stateService,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    IClearMLQueueService clearMLQueueService
) : IWordAlignmentEngineService
{
    private readonly IDistributedReaderWriterLockFactory _lockFactory = lockFactory;
    private readonly IPlatformService _platformService = platformServices.First(ps =>
        ps.EngineGroup == EngineGroup.WordAlignment
    );
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<WordAlignmentEngine> _engines = engines;
    private readonly WordAlignmentEngineStateService _stateService = stateService;
    private readonly IBuildJobService<WordAlignmentEngine> _buildJobService = buildJobService;
    private readonly IClearMLQueueService _clearMLQueueService = clearMLQueueService;

    public EngineType Type => EngineType.Statistical;

    public async Task<WordAlignmentEngine> CreateAsync(
        string engineId,
        string? engineName,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        WordAlignmentEngine wordAlignmentEngine = await _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                var waEngine = new WordAlignmentEngine
                {
                    EngineId = engineId,
                    SourceLanguage = sourceLanguage,
                    TargetLanguage = targetLanguage,
                    Type = EngineType.Statistical,
                };
                await _engines.InsertAsync(waEngine, ct);
                await _buildJobService.CreateEngineAsync(engineId, engineName, ct);
                return waEngine;
            },
            cancellationToken: cancellationToken
        );

        WordAlignmentEngineState state = _stateService.Get(engineId);
        state.InitNew();
        return wordAlignmentEngine;
    }

    public async Task<WordAlignmentResult> GetBestPhraseAlignmentAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        CancellationToken cancellationToken = default
    )
    {
        WordAlignmentEngine engine = await GetBuiltEngineAsync(engineId, cancellationToken);
        WordAlignmentEngineState state = _stateService.Get(engineId);

        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId, cancellationToken);
        WordAlignmentResult result = await @lock.ReaderLockAsync(
            async ct =>
            {
                IWordAlignmentEngine wordAlignmentEngine = await state.GetEngineAsync(engine.BuildRevision, ct);
                LatinWordTokenizer tokenizer = new();

                // there is no way to cancel this call
                IReadOnlyList<string> sourceTokens = tokenizer.Tokenize(sourceSegment).ToList();
                IReadOnlyList<string> targetTokens = tokenizer.Tokenize(targetSegment).ToList();
                IReadOnlyCollection<AlignedWordPair> wordPairs = wordAlignmentEngine.GetBestAlignedWordPairs(
                    sourceTokens,
                    targetTokens
                );
                wordAlignmentEngine.ComputeAlignedWordPairScores(sourceTokens, targetTokens, wordPairs);
                return new WordAlignmentResult(
                    sourceTokens: sourceTokens,
                    targetTokens: targetTokens,
                    alignment: new WordAlignmentMatrix(
                        sourceTokens.Count,
                        targetTokens.Count,
                        wordPairs.Select(wp => (wp.SourceIndex, wp.TargetIndex))
                    ),
                    confidences: wordPairs.Select(wp => wp.AlignmentScore * wp.TranslationScore).ToList()
                );
            },
            cancellationToken: cancellationToken
        );

        state.Touch();
        return result;

        throw new NotImplementedException();
    }

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        await CancelBuildJobAsync(engineId, cancellationToken);

        await _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                await _engines.DeleteAsync(e => e.EngineId == engineId, ct);
            },
            cancellationToken: cancellationToken
        );
        await _buildJobService.DeleteEngineAsync(engineId, CancellationToken.None);

        WordAlignmentEngineState state = _stateService.Get(engineId);
        _stateService.Remove(engineId);
        // there is no way to cancel this call
        state.DeleteData();
        state.Dispose();
        await _lockFactory.DeleteAsync(engineId, CancellationToken.None);
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
            EngineType.Statistical,
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

        WordAlignmentEngineState state = _stateService.Get(engineId);
        state.Touch();
    }

    public async Task CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        bool building = await CancelBuildJobAsync(engineId, cancellationToken);
        if (!building)
            throw new InvalidOperationException("The engine is not currently building.");

        WordAlignmentEngineState state = _stateService.Get(engineId);
        state.Touch();
    }

    public int GetQueueSize()
    {
        return _clearMLQueueService.GetQueueSize(Type);
    }

    private async Task<bool> CancelBuildJobAsync(string engineId, CancellationToken cancellationToken)
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
        return buildId is not null;
    }

    private async Task<WordAlignmentEngine> GetEngineAsync(string engineId, CancellationToken cancellationToken)
    {
        WordAlignmentEngine? engine = await _engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
        if (engine is null)
            throw new InvalidOperationException($"The engine {engineId} does not exist.");
        return engine;
    }

    private async Task<WordAlignmentEngine> GetBuiltEngineAsync(string engineId, CancellationToken cancellationToken)
    {
        WordAlignmentEngine engine = await GetEngineAsync(engineId, cancellationToken);
        if (engine.BuildRevision == 0)
            throw new EngineNotBuiltException("The engine must be built first.");
        return engine;
    }
}