﻿using SIL.ObjectModel;

namespace Serval.Machine.Shared.Services;

public class WordAlignmentEngineStateService(
    IWordAlignmentModelFactory wordAlignmentModelFactory,
    IOptionsMonitor<WordAlignmentEngineOptions> options,
    ILogger<WordAlignmentEngineStateService> logger
) : DisposableBase
{
    private readonly IWordAlignmentModelFactory _wordAlignmentModelFactory = wordAlignmentModelFactory;
    private readonly IOptionsMonitor<WordAlignmentEngineOptions> _options = options;
    private readonly ILogger<WordAlignmentEngineStateService> _logger = logger;

    private readonly ConcurrentDictionary<string, WordAlignmentEngineState> _engineStates =
        new ConcurrentDictionary<string, WordAlignmentEngineState>();

    public WordAlignmentEngineState Get(string engineId)
    {
        return _engineStates.GetOrAdd(engineId, CreateState);
    }

    public void Remove(string engineId)
    {
        _engineStates.TryRemove(engineId, out _);
    }

    public async Task CommitAsync(
        IDistributedReaderWriterLockFactory lockFactory,
        IRepository<TranslationEngine> engines,
        TimeSpan inactiveTimeout,
        CancellationToken cancellationToken = default
    )
    {
        foreach (WordAlignmentEngineState state in _engineStates.Values)
        {
            try
            {
                IDistributedReaderWriterLock @lock = await lockFactory.CreateAsync(state.EngineId, cancellationToken);
                await @lock.WriterLockAsync(
                    async ct =>
                    {
                        TranslationEngine? engine = await engines.GetAsync(state.EngineId, ct);
                        if (engine is not null && !(engine.CollectTrainSegmentPairs ?? false))
                            // there is no way to cancel this call
                            state.Commit(engine.BuildRevision, inactiveTimeout);
                    },
                    _options.CurrentValue.EngineCommitTimeout,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while committing SMT transfer engine {EngineId}.", state.EngineId);
            }
        }
    }

    private WordAlignmentEngineState CreateState(string engineId)
    {
        return new WordAlignmentEngineState(_wordAlignmentModelFactory, _options, engineId);
    }

    protected override void DisposeManagedResources()
    {
        foreach (WordAlignmentEngineState state in _engineStates.Values)
            state.Dispose();
        _engineStates.Clear();
    }
}