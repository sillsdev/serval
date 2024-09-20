namespace Serval.Shared.Services;

public class JobService<TJob>(IRepository<TJob> jobs) : EntityServiceBase<TJob>(jobs), IJobService<TJob>
    where TJob : IJob
{
    public async Task<IEnumerable<TJob>> GetAllAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(e => e.EngineRef == parentId, cancellationToken);
    }

    public Task<TJob?> GetActiveAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return Entities.GetAsync(
            b => b.EngineRef == parentId && (b.State == JobState.Active || b.State == JobState.Pending),
            cancellationToken
        );
    }

    public Task<EntityChange<TJob>> GetNewerRevisionAsync(
        string id,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        return GetNewerRevisionAsync(e => e.Id == id, minRevision, cancellationToken);
    }

    public Task<EntityChange<TJob>> GetActiveNewerRevisionAsync(
        string parentId,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        return GetNewerRevisionAsync(
            b => b.EngineRef == parentId && (b.State == JobState.Active || b.State == JobState.Pending),
            minRevision,
            cancellationToken
        );
    }

    private async Task<EntityChange<TJob>> GetNewerRevisionAsync(
        Expression<Func<TJob, bool>> filter,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        using ISubscription<TJob> subscription = await Entities.SubscribeAsync(filter, cancellationToken);
        EntityChange<TJob> curChange = subscription.Change;
        if (curChange.Type == EntityChangeType.Delete && minRevision > 1)
            return curChange;
        while (true)
        {
            if (curChange.Entity is not null)
            {
                if (curChange.Type != EntityChangeType.Delete && minRevision <= curChange.Entity.Revision)
                    return curChange;
            }
            await subscription.WaitForChangeAsync(cancellationToken: cancellationToken);
            curChange = subscription.Change;
            if (curChange.Type == EntityChangeType.Delete)
                return curChange;
        }
    }
}
