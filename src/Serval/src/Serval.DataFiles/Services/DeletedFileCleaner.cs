namespace Serval.DataFiles.Services;

public class DeletedFileCleaner : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<DataFileOptions> _options;
    private readonly CronExpression _cronExpression;
    private readonly ILogger<DeletedFileCleaner> _logger;
    private readonly IFileSystem _fileSystem;

    public DeletedFileCleaner(
        IServiceProvider serviceProvider,
        IOptionsMonitor<DataFileOptions> options,
        ILogger<DeletedFileCleaner> logger,
        IFileSystem fileSystem
    )
    {
        _serviceProvider = serviceProvider;
        _options = options;
        CronFormat cronFormat = CronFormat.Standard;
        if (_options.CurrentValue.DeletedFileCleanerSchedule.Trim().Split().Length == 6)
            cronFormat = CronFormat.IncludeSeconds;
        _cronExpression = CronExpression.Parse(_options.CurrentValue.DeletedFileCleanerSchedule, cronFormat);
        _logger = logger;
        _fileSystem = fileSystem;
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
                DateTimeOffset? next = _cronExpression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
                Debug.Assert(next.HasValue);
                await Task.Delay(next.Value - DateTimeOffset.Now, stoppingToken);
                await CleanAsync(stoppingToken);
            }
        }
        catch (TaskCanceledException) { }
    }

    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning deleted files.");
        using IServiceScope scope = _serviceProvider.CreateScope();
        IRepository<DeletedFile> deletedFiles = scope.ServiceProvider.GetRequiredService<IRepository<DeletedFile>>();
        DateTime checkDateTime = DateTime.UtcNow - _options.CurrentValue.DeletedFileTimeout;
        IReadOnlyList<DeletedFile> deletedFilesToClean = await deletedFiles.GetAllAsync(
            f => f.DeletedAt <= checkDateTime,
            cancellationToken
        );

        var deletedFileIds = new HashSet<string>();
        foreach (DeletedFile deletedFile in deletedFilesToClean)
        {
            string path = GetDataFilePath(deletedFile.Filename);
            _fileSystem.DeleteFile(path);
            deletedFileIds.Add(deletedFile.Id);
        }
        await deletedFiles.DeleteAllAsync(f => deletedFileIds.Contains(f.Id), cancellationToken);
        _logger.LogInformation("Cleaned {count} files.", deletedFileIds.Count);
    }

    private string GetDataFilePath(string filename)
    {
        return Path.Combine(_options.CurrentValue.FilesDirectory, filename);
    }
}
