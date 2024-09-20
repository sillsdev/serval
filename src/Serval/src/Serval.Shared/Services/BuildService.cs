namespace Serval.Shared.Services;

public class BuildService<TBuild>(IRepository<TBuild> jobs) : EntityServiceBase<TBuild>(jobs), IBuildService<TBuild>
    where TBuild : IBuild
{
    public async Task<IEnumerable<TBuild>> GetAllAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(e => e.EngineRef == parentId, cancellationToken);
    }

    public Task<TBuild?> GetActiveAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return Entities.GetAsync(
            b => b.EngineRef == parentId && (b.State == BuildState.Active || b.State == BuildState.Pending),
            cancellationToken
        );
    }

    public Task<EntityChange<TBuild>> GetNewerRevisionAsync(
        string id,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        return GetNewerRevisionAsync(e => e.Id == id, minRevision, cancellationToken);
    }

    public Task<EntityChange<TBuild>> GetActiveNewerRevisionAsync(
        string parentId,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        return GetNewerRevisionAsync(
            b => b.EngineRef == parentId && (b.State == BuildState.Active || b.State == BuildState.Pending),
            minRevision,
            cancellationToken
        );
    }

    private async Task<EntityChange<TBuild>> GetNewerRevisionAsync(
        Expression<Func<TBuild, bool>> filter,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        using ISubscription<TBuild> subscription = await Entities.SubscribeAsync(filter, cancellationToken);
        EntityChange<TBuild> curChange = subscription.Change;
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
