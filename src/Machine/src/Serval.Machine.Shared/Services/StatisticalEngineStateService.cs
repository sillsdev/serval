using SIL.ObjectModel;

namespace Serval.Machine.Shared.Services;

public class StatisticalEngineStateService(
    IWordAlignmentModelFactory wordAlignmentModelFactory,
    IOptionsMonitor<StatisticalWordAlignmentEngineOptions> options,
    ILogger<StatisticalEngineStateService> logger
) : DisposableBase
{
    private readonly IWordAlignmentModelFactory _wordAlignmentModelFactory = wordAlignmentModelFactory;
    private readonly IOptionsMonitor<StatisticalWordAlignmentEngineOptions> _options = options;
    private readonly ILogger<StatisticalEngineStateService> _logger = logger;

    private readonly ConcurrentDictionary<string, StatisticalEngineState> _engineStates =
        new ConcurrentDictionary<string, StatisticalEngineState>();

    public StatisticalEngineState Get(string engineId)
    {
        return _engineStates.GetOrAdd(engineId, CreateState);
    }

    public void Remove(string engineId)
    {
        _engineStates.TryRemove(engineId, out _);
    }

    public async Task CommitAsync(
        IDistributedReaderWriterLockFactory lockFactory,
        IRepository<WordAlignmentEngine> engines,
        TimeSpan inactiveTimeout,
        CancellationToken cancellationToken = default
    )
    {
        foreach (StatisticalEngineState state in _engineStates.Values)
        {
            if (!state.IsLoaded || state.IsMarkedForDeletion)
            {
                continue;
            }

            try
            {
                IDistributedReaderWriterLock @lock = await lockFactory.CreateAsync(state.EngineId, cancellationToken);
                await @lock.WriterLockAsync(
                    async ct =>
                    {
                        WordAlignmentEngine? engine = await engines.GetAsync(state.EngineId, ct);
                        if (engine is not null)
                            // there is no way to cancel this call
                            state.Commit(engine!.BuildRevision, inactiveTimeout);
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

    private StatisticalEngineState CreateState(string engineId)
    {
        return new StatisticalEngineState(_wordAlignmentModelFactory, _options, engineId);
    }

    protected override void DisposeManagedResources()
    {
        foreach (StatisticalEngineState state in _engineStates.Values)
            state.Dispose();
        _engineStates.Clear();
    }
}
