namespace Serval.Shared.Services;

public abstract class OwnedEntityServiceBase<T>(IRepository<T> entities) : EntityServiceBase<T>(entities)
    where T : IOwnedEntity
{
    public virtual async Task<IEnumerable<T>> GetAllAsync(string owner, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(e => e.Owner == owner, cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default
    )
    {
        HashSet<string> idSet = ids.ToHashSet();
        return await Entities.GetAllAsync(e => idSet.Contains(e.Id), cancellationToken);
    }
}
