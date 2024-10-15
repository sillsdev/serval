using Microsoft.Extensions.DependencyInjection;
using SIL.ServiceToolkit.Services;

namespace Serval.Translation.Services;

public class EngineCleanupService(
    IServiceProvider services,
    ILogger<EngineCleanupService> logger,
    TimeSpan? timeout = null
) : RecurrentTask("Engine Cleanup Service", services, RefreshPeriod, logger)
{
    private readonly ILogger<EngineCleanupService> _logger = logger;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RefreshPeriod = TimeSpan.FromDays(1);

    protected override async Task DoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running engine cleanup job");
        var engines = scope.ServiceProvider.GetRequiredService<IRepository<Engine>>();
        var engineService = scope.ServiceProvider.GetRequiredService<EngineService>();
        await CheckEnginesAsync(engines, engineService, cancellationToken);
    }

    public async Task CheckEnginesAsync(
        IRepository<Engine> engines,
        EngineService engineService,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<Engine> allEngines = await engines.GetAllAsync(cancellationToken);
        IEnumerable<Engine> notCreatedEngines = allEngines.Where(e => !e.SuccessfullyCreated);
        await Task.Delay(_timeout, cancellationToken); //Make sure the engines are not midway through being created
        foreach (
            Engine engine in await engines.GetAllAsync(
                e => notCreatedEngines.Select(f => f.Id).Contains(e.Id),
                cancellationToken
            )
        )
        {
            if (!engine.SuccessfullyCreated)
            {
                _logger.LogInformation("Deleting engine {id} because it was never successfully created", engine.Id);
                await engineService.DeleteAsync(engine.Id, cancellationToken);
            }
        }
    }
}
