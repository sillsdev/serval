namespace Serval.WordAlignment.Services;

public class BuildCleanupService(
    IServiceProvider services,
    ILogger<BuildCleanupService> logger,
    TimeSpan? timeout = null
) : UninitializedCleanupService<Build>(services, logger, timeout) { }
