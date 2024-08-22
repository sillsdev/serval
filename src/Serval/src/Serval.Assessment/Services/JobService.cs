namespace Serval.Assessment.Services;

public class JobService(IDataAccessContext dataAccessContext, IRepository<Job> jobs, IRepository<Result> results)
    : EntityServiceBase<Job>(jobs),
        IJobService
{
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<Result> _results = results;

    public async Task<IEnumerable<Job>> GetAllAsync(string engineId, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(e => e.EngineRef == engineId, cancellationToken);
    }

    public override Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                Job? job = await Entities.DeleteAsync(id, ct);
                if (job is null)
                    throw new EntityNotFoundException($"Could not find the Job '{id}'.");

                await _results.DeleteAllAsync(r => r.JobRef == id, ct);
            },
            cancellationToken
        );
    }

    public Task<EntityChange<Job>> GetNewerRevisionAsync(
        string id,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        return GetNewerRevisionAsync(e => e.Id == id, minRevision, cancellationToken);
    }

    private async Task<EntityChange<Job>> GetNewerRevisionAsync(
        Expression<Func<Job, bool>> filter,
        long minRevision,
        CancellationToken cancellationToken = default
    )
    {
        using ISubscription<Job> subscription = await Entities.SubscribeAsync(filter, cancellationToken);
        EntityChange<Job> curChange = subscription.Change;
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
