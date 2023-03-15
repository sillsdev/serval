namespace Serval.Shared.Services;

public class EntityServiceBase<T>
    where T : IEntity
{
    protected EntityServiceBase(IRepository<T> entities)
    {
        Entities = entities;
    }

    protected IRepository<T> Entities { get; }

    public Task<T?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return Entities.GetAsync(id, cancellationToken);
    }

    public virtual Task CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Entities.InsertAsync(entity, cancellationToken);
    }

    public virtual async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Entities.DeleteAsync(id, cancellationToken) is not null;
    }
}
