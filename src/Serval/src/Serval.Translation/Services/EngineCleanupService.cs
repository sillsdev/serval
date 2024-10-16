using Microsoft.Extensions.DependencyInjection;

namespace Serval.Translation.Services;

public class EngineCleanupService(
    IServiceProvider services,
    ILogger<EngineCleanupService> logger,
    TimeSpan? timeout = null
) : UninitializedCleanupService<Engine>(services, logger, timeout)
{
    public EngineService? EngineService { get; set; }

    protected override async Task DoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        EngineService = scope.ServiceProvider.GetRequiredService<EngineService>();
        await base.DoWorkAsync(scope, cancellationToken);
    }

    protected override async Task DeleteEntityAsync(
        IRepository<Engine> engines,
        Engine engine,
        CancellationToken cancellationToken
    )
    {
        if (EngineService == null)
            return;
        await EngineService.DeleteAsync(engine.Id, cancellationToken);
    }
}
