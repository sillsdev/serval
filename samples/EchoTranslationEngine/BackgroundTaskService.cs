namespace EchoTranslationEngine;

public class BackgroundTaskService : BackgroundService
{
    private readonly ILogger<BackgroundTaskService> _logger;
    private readonly BackgroundTaskQueue _taskQueue;
    private readonly IServiceProvider _services;

    public BackgroundTaskService(
        BackgroundTaskQueue taskQueue,
        ILogger<BackgroundTaskService> logger,
        IServiceProvider services
    )
    {
        _taskQueue = taskQueue;
        _logger = logger;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Background Task Service is running.");
        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);
            try
            {
                using var scope = _services.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing {WorkItem}.", nameof(workItem));
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Task Service is stopping.");
        await base.StopAsync(stoppingToken);
    }
}
