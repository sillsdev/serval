namespace SIL.ServiceToolkit.Services;

public class BackgroundTaskService(
    BackgroundTaskQueue taskQueue,
    ILogger<BackgroundTaskService> logger,
    IServiceProvider services
) : BackgroundService
{
    private readonly ILogger<BackgroundTaskService> _logger = logger;
    private readonly BackgroundTaskQueue _taskQueue = taskQueue;
    private readonly IServiceProvider _services = services;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Background Task Service is running.");
        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, ValueTask> workItem = await _taskQueue.DequeueAsync(
                stoppingToken
            );
            try
            {
                using IServiceScope scope = _services.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing {WorkItem}.", nameof(workItem));
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Background Task Service is stopping.");
        await base.StopAsync(cancellationToken);
    }
}
