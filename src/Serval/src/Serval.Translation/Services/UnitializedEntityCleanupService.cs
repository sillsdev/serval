using Microsoft.Extensions.DependencyInjection;
using SIL.ServiceToolkit.Services;

namespace Serval.Translation.Services;

public abstract class UninitializedCleanupService<T>(
    IServiceProvider services,
    ILogger<UninitializedCleanupService<T>> logger,
    TimeSpan? timeout = null
) : RecurrentTask($"{typeof(T)} Cleanup Service", services, RefreshPeriod, logger)
    where T : IInitializableEntity
{
    private readonly ILogger<UninitializedCleanupService<T>> _logger = logger;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RefreshPeriod = TimeSpan.FromDays(1);

    protected override async Task DoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running build cleanup job");
        var entities = scope.ServiceProvider.GetRequiredService<IRepository<T>>();
        await CheckEntitiesAsync(entities, cancellationToken);
    }

    public async Task CheckEntitiesAsync(IRepository<T> entities, CancellationToken cancellationToken)
    {
        IReadOnlyList<T> allEntities = await entities.GetAllAsync(cancellationToken);
        var now = DateTime.UtcNow;
        IEnumerable<T> uninitializedEntities = allEntities.Where(b =>
            !(b.IsInitialized ?? true) && (now - (b.DateCreated ?? DateTime.UtcNow)) > _timeout
        );
        foreach (T entity in uninitializedEntities)
        {
            _logger.LogInformation(
                "Deleting {type} {id} because it was never successfully started",
                typeof(T),
                entity.Id
            );
            await DeleteEntityAsync(entities, entity, cancellationToken);
        }
    }

    protected virtual async Task DeleteEntityAsync(
        IRepository<T> entities,
        T entity,
        CancellationToken cancellationToken
    )
    {
        await entities.DeleteAsync(e => e.Id == entity.Id, cancellationToken);
    }
}
