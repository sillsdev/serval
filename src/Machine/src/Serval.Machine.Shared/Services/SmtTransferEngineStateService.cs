using SIL.ObjectModel;

namespace Serval.Machine.Shared.Services;

public class SmtTransferEngineStateService(
    ISmtModelFactory smtModelFactory,
    ITransferEngineFactory transferEngineFactory,
    ITruecaserFactory truecaserFactory,
    IOptionsMonitor<SmtTransferEngineOptions> options,
    ILogger<SmtTransferEngineStateService> logger
) : DisposableBase
{
    private readonly ISmtModelFactory _smtModelFactory = smtModelFactory;
    private readonly ITransferEngineFactory _transferEngineFactory = transferEngineFactory;
    private readonly ITruecaserFactory _truecaserFactory = truecaserFactory;
    private readonly IOptionsMonitor<SmtTransferEngineOptions> _options = options;
    private readonly ILogger<SmtTransferEngineStateService> _logger = logger;

    private readonly ConcurrentDictionary<string, SmtTransferEngineState> _engineStates =
        new ConcurrentDictionary<string, SmtTransferEngineState>();

    public SmtTransferEngineState Get(string engineId)
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
        foreach (SmtTransferEngineState state in _engineStates.Values)
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

    private SmtTransferEngineState CreateState(string engineId)
    {
        return new SmtTransferEngineState(
            _smtModelFactory,
            _transferEngineFactory,
            _truecaserFactory,
            _options,
            engineId
        );
    }

    protected override void DisposeManagedResources()
    {
        foreach (SmtTransferEngineState state in _engineStates.Values)
            state.Dispose();
        _engineStates.Clear();
    }
}
