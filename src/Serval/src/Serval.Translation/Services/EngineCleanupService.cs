using Microsoft.Extensions.DependencyInjection;
using SIL.ServiceToolkit.Services;

namespace Serval.Translation.Services;

public class EngineCleanupService(IServiceProvider services, ILogger<EngineCleanupService> logger)
    : RecurrentTask("Engine Cleanup Service", services, RefreshPeriod, logger)
{
    private readonly ILogger<EngineCleanupService> _logger = logger;
    private static readonly TimeSpan RefreshPeriod = TimeSpan.FromDays(1);

    protected override async Task DoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running engine cleanup job");
        var engines = scope.ServiceProvider.GetRequiredService<IRepository<Engine>>();
        var engineService = scope.ServiceProvider.GetRequiredService<EngineService>();
        IReadOnlyList<Engine> allEngines = await engines.GetAllAsync(cancellationToken);
        IEnumerable<Engine> notCreatedEngines = allEngines.Where(e => !e.SuccessfullyCreated);
        await Task.Delay(120, cancellationToken); //Make sure the engines are not midway through being created
        foreach (Engine engine in notCreatedEngines)
        {
            if (!engine.SuccessfullyCreated)
            {
                _logger.LogInformation("Deleting engine {id} because it was never successfully created", engine.Id);
                await engineService.DeleteAsync(engine.Id, cancellationToken);
            }
        }
    }
}
