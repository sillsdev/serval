namespace SIL.DataAccess;

public class MongoRepository<T> : IRepository<T>
    where T : IEntity
{
    private readonly IMongoDataAccessContext _context;
    private readonly IMongoCollection<T> _collection;

    public MongoRepository(IMongoDataAccessContext context, IMongoCollection<T> collection)
    {
        _context = context;
        _collection = collection;
    }

    public async Task<T?> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        if (_context.Session is not null)
        {
            return await _collection
                .AsQueryable(_context.Session)
                .FirstOrDefaultAsync(filter, cancellationToken)
                .ConfigureAwait(false);
        }
        return await _collection.AsQueryable().FirstOrDefaultAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        if (_context.Session is not null)
        {
            return await _collection
                .AsQueryable(_context.Session)
                .Where(filter)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        return await _collection.AsQueryable().Where(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        if (_context.Session is not null)
        {
            return await _collection
                .AsQueryable(_context.Session)
                .AnyAsync(filter, cancellationToken)
                .ConfigureAwait(false);
        }
        return await _collection.AsQueryable().AnyAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    public async Task InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.Revision = 1;
        try
        {
            if (_context.Session is not null)
            {
                await _collection
                    .InsertOneAsync(_context.Session, entity, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
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
            if (_context.Session is not null)
            {
                await _collection
                    .InsertManyAsync(_context.Session, entities, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _collection.InsertManyAsync(entities, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
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
            var options = new FindOneAndUpdateOptions<T> { IsUpsert = upsert, ReturnDocument = ReturnDocument.After };
            T? entity;
            if (_context.Session is not null)
            {
                entity = await _collection
                    .FindOneAndUpdateAsync(_context.Session, filter, updateDef, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                entity = await _collection
                    .FindOneAndUpdateAsync(filter, updateDef, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            return entity;
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
                throw new DuplicateKeyException();
            throw;
        }
    }

    public async Task<int> UpdateAllAsync(
        Expression<Func<T, bool>> filter,
        Action<IUpdateBuilder<T>> update,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var updateBuilder = new MongoUpdateBuilder<T>();
            update(updateBuilder);
            updateBuilder.Inc(e => e.Revision, 1);
            UpdateDefinition<T> updateDef = updateBuilder.Build();
            UpdateResult result;
            if (_context.Session is not null)
            {
                result = await _collection
                    .UpdateManyAsync(_context.Session, filter, updateDef, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                result = await _collection
                    .UpdateManyAsync(filter, updateDef, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            return (int)result.ModifiedCount;
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
        if (_context.Session is not null)
        {
            return await _collection
                .FindOneAndDeleteAsync(_context.Session, filter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        return await _collection
            .FindOneAndDeleteAsync(filter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> DeleteAllAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        DeleteResult result;
        if (_context.Session is not null)
        {
            result = await _collection
                .DeleteManyAsync(_context.Session, filter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            result = await _collection
                .DeleteManyAsync(filter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        return (int)result.DeletedCount;
    }

    public async Task<ISubscription<T>> SubscribeAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        IMongoClient client = _collection.Database.Client;
        IMongoCollection<BsonDocument> oplog = client.GetDatabase("local").GetCollection<BsonDocument>("oplog.rs");
        var filterDef = FilterDefinition<BsonDocument>.Empty;
        SortDefinition<BsonDocument> sortDef = Builders<BsonDocument>.Sort.Descending("$natural");
        Expression<Func<BsonDocument, BsonValue>> projectDef = d => d["ts"];
        BsonTimestamp timestamp;
        if (_context.Session is not null)
        {
            timestamp = (BsonTimestamp)
                await oplog
                    .Find(_context.Session, filterDef)
                    .Sort(sortDef)
                    .Project(projectDef)
                    .FirstAsync(cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            timestamp = (BsonTimestamp)
                await oplog
                    .Find(filterDef)
                    .Sort(sortDef)
                    .Project(projectDef)
                    .FirstAsync(cancellationToken)
                    .ConfigureAwait(false);
        }
        T? initialEntity = await GetAsync(filter, cancellationToken).ConfigureAwait(false);
        var subscription = new MongoSubscription<T>(_context, _collection, filter.Compile(), timestamp, initialEntity);
        return subscription;
    }
}
