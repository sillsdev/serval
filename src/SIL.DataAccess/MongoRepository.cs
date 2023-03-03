namespace SIL.DataAccess;

public class MongoRepository<T> : IRepository<T>
    where T : IEntity
{
    private readonly IMongoCollection<T> _collection;

    public MongoRepository(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    public async Task<T?> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        return await _collection.AsQueryable().FirstOrDefaultAsync(filter, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        return await _collection.AsQueryable().Where(filter).ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        return _collection.AsQueryable().AnyAsync(filter, cancellationToken);
    }

    public async Task InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.Revision = 1;
        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
                throw new DuplicateKeyException(e);
            throw;
        }
    }

    public async Task InsertAllAsync(IReadOnlyCollection<T> entities, CancellationToken cancellationToken = default)
    {
        foreach (T entity in entities)
            entity.Revision = 1;

        try
        {
            await _collection.InsertManyAsync(entities, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
                throw new DuplicateKeyException(e);
            throw;
        }
    }

    public async Task<T?> UpdateAsync(
        Expression<Func<T, bool>> filter,
        Action<IUpdateBuilder<T>> update,
        bool upsert = false,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var updateBuilder = new MongoUpdateBuilder<T>();
            update(updateBuilder);
            updateBuilder.Inc(e => e.Revision, 1);
            UpdateDefinition<T> updateDef = updateBuilder.Build();
            T? entity = await _collection.FindOneAndUpdateAsync(
                filter,
                updateDef,
                new FindOneAndUpdateOptions<T> { IsUpsert = upsert, ReturnDocument = ReturnDocument.After },
                cancellationToken
            );
            return entity;
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
                throw new DuplicateKeyException();
            throw;
        }
    }

    public async Task<T?> DeleteAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        T? entity = await _collection.FindOneAndDeleteAsync(filter, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<int> DeleteAllAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        DeleteResult result = await _collection.DeleteManyAsync(filter, cancellationToken);
        return (int)result.DeletedCount;
    }

    public async Task<ISubscription<T>> SubscribeAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        var currentTime = DateTime.UtcNow;
        T initialEntity = await _collection.AsQueryable().FirstOrDefaultAsync(filter, cancellationToken);
        var subscription = new MongoSubscription<T>(_collection, filter.Compile(), currentTime, initialEntity);
        return subscription;
    }
}
