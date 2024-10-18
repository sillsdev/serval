﻿namespace Serval.Translation.Services;

public class BuildService(IRepository<Build> builds) : EntityServiceBase<Build>(builds), IBuildService
{
    public async Task<IEnumerable<Build>> GetAllAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(
            e => e.EngineRef == parentId && (e.IsInitialized == null || e.IsInitialized.Value),
            cancellationToken
        );
    }

    public Task<Build?> GetActiveAsync(string parentId, CancellationToken cancellationToken = default)
    {
        return Entities.GetAsync(
            b =>
                b.EngineRef == parentId
                && (b.IsInitialized == null || b.IsInitialized.Value)
                && (b.State == JobState.Active || b.State == JobState.Pending),
            cancellationToken
        );
    }

    public Task<EntityChange<Build>> GetNewerRevisionAsync(
        string id,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        return GetNewerRevisionAsync(
            e => e.Id == id && (e.IsInitialized == null || e.IsInitialized.Value),
            minRevision,
            cancellationToken
        );
    }

    public Task<EntityChange<Build>> GetActiveNewerRevisionAsync(
        string parentId,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        return GetNewerRevisionAsync(
            b =>
                b.EngineRef == parentId
                && (b.IsInitialized == null || b.IsInitialized.Value)
                && (b.State == JobState.Active || b.State == JobState.Pending),
            minRevision,
            cancellationToken
        );
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
