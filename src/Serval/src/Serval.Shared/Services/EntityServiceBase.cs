namespace Serval.Shared.Services;

public class EntityServiceBase<T>
    where T : IEntity
{
    protected EntityServiceBase(IRepository<T> entities)
    {
        Entities = entities;
    }

    protected IRepository<T> Entities { get; }

    public async Task<T> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        T? entity = await Entities.GetAsync(id, cancellationToken);
        if (entity is null)
            throw new EntityNotFoundException($"Could not find the {typeof(T).Name} '{id}'.");
        return entity;
    }

    public virtual Task CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Entities.InsertAsync(entity, cancellationToken);
    }

    public virtual async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        T? entity = await Entities.DeleteAsync(id, cancellationToken);
        if (entity is null)
            throw new EntityNotFoundException($"Could not find the {typeof(T).Name} '{id}'.");
    }
}
