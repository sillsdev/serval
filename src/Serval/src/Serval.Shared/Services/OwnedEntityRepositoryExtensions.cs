namespace Serval.Shared.Services;

public static class OwnedEntityRepositoryExtensions
{
    public static async Task<T> CheckOwnerAsync<T>(
        this IRepository<T> repository,
        string id,
        string owner,
        CancellationToken cancellationToken
    )
        where T : IOwnedEntity
    {
        T? entity = await repository.GetAsync(id, cancellationToken);
        if (entity is null)
            throw new EntityNotFoundException($"Could not find the {typeof(T).Name} '{id}'.");
        if (entity.Owner != owner)
            throw new ForbiddenException();
        return entity;
    }
}
