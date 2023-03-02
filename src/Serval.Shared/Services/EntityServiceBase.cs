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

    public virtual Task CreateAsync(T entity)
    {
        return Entities.InsertAsync(entity);
    }

    public virtual async Task<bool> DeleteAsync(string id)
    {
        return await Entities.DeleteAsync(id) is not null;
    }
}
