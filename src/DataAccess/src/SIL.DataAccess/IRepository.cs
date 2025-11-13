namespace SIL.DataAccess;

public interface IRepository<T>
    where T : IEntity
{
    Task<T?> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllWithJoinAsync<T2, TKey>(
        Expression<Func<T, bool>> thisFilter,
        Expression<Func<T2, bool>> otherFilter,
        IRepository<T2> otherRepository,
        Expression<Func<T, TKey>> thisKey,
        Expression<Func<T2, TKey>> otherKey,
        CancellationToken cancellationToken = default
    )
        where T2 : IEntity;
    Task<bool> ExistsAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    Task InsertAsync(T entity, CancellationToken cancellationToken = default);
    Task InsertAllAsync(IReadOnlyCollection<T> entities, CancellationToken cancellationToken = default);

    Task<T?> UpdateAsync(
        Expression<Func<T, bool>> filter,
        Action<IUpdateBuilder<T>> update,
        bool upsert = false,
        bool returnOriginal = false,
        CancellationToken cancellationToken = default
    );
    Task<int> UpdateAllAsync(
        Expression<Func<T, bool>> filter,
        Action<IUpdateBuilder<T>> update,
        CancellationToken cancellationToken = default
    );

    Task<T?> DeleteAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    Task<int> DeleteAllAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    Task<ISubscription<T>> SubscribeAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    );
}
