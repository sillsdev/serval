namespace Serval.Translation.Features.Engines;

internal static class BuildRepositoryExtensions
{
    internal static async Task<EntityChange<Build>> GetNewerRevisionAsync(
        this IRepository<Build> repository,
        Expression<Func<Build, bool>> filter,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        using ISubscription<Build> subscription = await repository.SubscribeAsync(filter, cancellationToken);
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
