namespace Serval.Translation.Services;

public class BuildService(IRepository<Build> builds) : OwnedEntityServiceBase<Build>(builds), IBuildService
{
    public async Task<IEnumerable<Build>> GetAllCreatedAfterAsync(
        string owner,
        DateTime? createdAfter,
        CancellationToken cancellationToken = default
    )
    {
        return await Entities.GetAllAsync(b => b.Owner == owner && b.DateCreated > createdAfter, cancellationToken);
    }

    public async Task<EntityChange<Build>> GetNextFinishedBuildAsync(
        string owner,
        DateTime finishedAfter,
        CancellationToken cancellationToken = default
    )
    {
        using ISubscription<Build> subscription = await Entities.SubscribeAsync(
            b =>
                b.Owner == owner
                && (b.State == JobState.Completed || b.State == JobState.Canceled || b.State == JobState.Faulted)
                && b.DateFinished > finishedAfter,
            cancellationToken
        );
        EntityChange<Build> curChange = subscription.Change;
        while (true)
        {
            if (curChange.Type is not EntityChangeType.None and not EntityChangeType.Delete)
                return curChange;
            await subscription.WaitForChangeAsync(
                changeTypes: new HashSet<EntityChangeType> { EntityChangeType.Insert, EntityChangeType.Update },
                cancellationToken: cancellationToken
            );
            curChange = subscription.Change;
        }
    }
}
