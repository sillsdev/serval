namespace Serval.Translation.Services;

public class BuildService(IRepository<Build> builds) : OwnedEntityServiceBase<Build>(builds), IBuildService
{
    public async Task<IEnumerable<Build>> GetAllAsync(
        string ownerId,
        string parentId,
        CancellationToken cancellationToken = default
    )
    {
        return await Entities.GetAllAsync(e => e.Owner == ownerId && e.EngineRef == parentId, cancellationToken);
    }

    public async Task<IEnumerable<Build>> GetAllCreatedAfterAsync(
        string owner,
        DateTime? createdAfter,
        CancellationToken cancellationToken = default
    )
    {
        return await Entities.GetAllAsync(b => b.Owner == owner && b.DateCreated > createdAfter, cancellationToken);
    }

    public Task<Build?> GetActiveAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return Entities.GetAsync(
            b => b.EngineRef == parentId && (b.State == JobState.Active || b.State == JobState.Pending),
            cancellationToken
        );
    }

    public Task<EntityChange<Build>> GetNewerRevisionAsync(
        string id,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        return GetNewerRevisionAsync(e => e.Id == id, minRevision, cancellationToken);
    }

    public Task<EntityChange<Build>> GetActiveNewerRevisionAsync(
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

    public async Task<EntityChange<Build>> GetNextCompletedBuildAsync(
        string owner,
        DateTime finishedAfter,
        CancellationToken cancellationToken = default
    )
    {
        using ISubscription<Build> subscription = await Entities.SubscribeAsync(
            b => b.Owner == owner && b.State == JobState.Completed && b.DateFinished > finishedAfter,
            cancellationToken
        );
        EntityChange<Build> curChange = subscription.Change;
        while (true)
        {
            if (curChange.Type is not EntityChangeType.None and not EntityChangeType.Delete)
                return curChange;
            await subscription.WaitForChangeAsync(insertsOrUpdatesOnly: true, cancellationToken: cancellationToken);
            curChange = subscription.Change;
        }
    }

    private async Task<EntityChange<Build>> GetNewerRevisionAsync(
        Expression<Func<Build, bool>> filter,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        using ISubscription<Build> subscription = await Entities.SubscribeAsync(filter, cancellationToken);
        EntityChange<Build> curChange = subscription.Change;
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
