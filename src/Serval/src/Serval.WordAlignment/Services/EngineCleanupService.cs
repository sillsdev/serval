namespace Serval.WordAlignment.Services;

public class EngineCleanupService(
    IServiceProvider services,
    ILogger<EngineCleanupService> logger,
    TimeSpan? timeout = null
) : UninitializedCleanupService<Engine>(services, logger, timeout) { }
