namespace Serval.Shared.Services;

public abstract class EntityServiceBase<T>(IRepository<T> entities)
    where T : IEntity
{
    protected IRepository<T> Entities { get; } = entities;

    public async Task<T> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        T? entity;
        try
        {
            entity = await Entities.GetAsync(id, cancellationToken);
        }
        catch (Exception e)
        {
            throw new EntityNotFoundException($"Could not find the {typeof(T).Name} '{id}'.", e);
        }
        if (entity is null)
            throw new EntityNotFoundException($"Could not find the {typeof(T).Name} '{id}'.");
        return entity;
    }

    public virtual async Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Entities.InsertAsync(entity, cancellationToken);
        return entity;
    }

    public virtual async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        T? entity = await Entities.DeleteAsync(id, cancellationToken);
        if (entity is null)
            throw new EntityNotFoundException($"Could not find the {typeof(T).Name} '{id}'.");
    }
}
