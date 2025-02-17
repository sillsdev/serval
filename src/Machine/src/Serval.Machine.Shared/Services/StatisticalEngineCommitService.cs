namespace Serval.Machine.Shared.Services;

public class StatisticalEngineCommitService(
    IServiceProvider services,
    IOptionsMonitor<StatisticalWordAlignmentEngineOptions> engineOptions,
    StatisticalEngineStateService stateService,
    ILogger<StatisticalEngineCommitService> logger
)
    : RecurrentTask(
        "SMT transfer engine commit service",
        services,
        engineOptions.CurrentValue.EngineCommitFrequency,
        logger
    )
{
    private readonly IOptionsMonitor<StatisticalWordAlignmentEngineOptions> _engineOptions = engineOptions;
    private readonly StatisticalEngineStateService _stateService = stateService;
    private readonly ILogger<StatisticalEngineCommitService> _logger = logger;

    protected override async Task DoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var engines = scope.ServiceProvider.GetRequiredService<IRepository<WordAlignmentEngine>>();
            var lockFactory = scope.ServiceProvider.GetRequiredService<IDistributedReaderWriterLockFactory>();
            await _stateService.CommitAsync(
                lockFactory,
                engines,
                _engineOptions.CurrentValue.InactiveEngineTimeout,
                cancellationToken
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while committing SMT transfer engines.");
        }
    }
}
