namespace Serval.Translation.Services;

public class DeletedPretranslationCleaner : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<PretranslationOptions> _options;
    private readonly CronExpression _cronExpression;
    private readonly ILogger<DeletedPretranslationCleaner> _logger;

    public DeletedPretranslationCleaner(
        IServiceProvider serviceProvider,
        IOptionsMonitor<PretranslationOptions> options,
        ILogger<DeletedPretranslationCleaner> logger
    )
    {
        _serviceProvider = serviceProvider;
        _options = options;
        CronFormat cronFormat = CronFormat.Standard;
        if (_options.CurrentValue.DeletedPretranslationCleanerSchedule.Trim().Split().Length == 6)
            cronFormat = CronFormat.IncludeSeconds;
        _cronExpression = CronExpression.Parse(_options.CurrentValue.DeletedPretranslationCleanerSchedule, cronFormat);
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await CleanAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                DateTimeOffset now = DateTimeOffset.Now;
                DateTimeOffset? next = _cronExpression.GetNextOccurrence(now, TimeZoneInfo.Local);
                Debug.Assert(next.HasValue);
                await Task.Delay(next.Value - now, stoppingToken);
                await CleanAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning deleted pretranslationss.");
        using IServiceScope scope = _serviceProvider.CreateScope();
        IRepository<Pretranslation> pretranslations = scope.ServiceProvider.GetRequiredService<
            IRepository<Pretranslation>
        >();
        DateTime checkDateTime = DateTime.UtcNow - _options.CurrentValue.DeletedPretranslationTimeout;
        IReadOnlyList<Pretranslation> pretranslationsToDelete = await pretranslations.GetAllAsync(
            f => f.DeletedAt <= checkDateTime,
            cancellationToken
        );
        int count = await pretranslations.DeleteAllAsync(
            f => f.DeletedAt <= checkDateTime,
            batchSize: 1000,
            cancellationToken: cancellationToken
        );
        _logger.LogInformation("Deleted {count} pretranslations.", count);
    }
}
