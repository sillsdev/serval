namespace SIL.DataAccess;

public class MongoRepository<T>(IMongoDataAccessContext context, IMongoCollection<T> collection) : IRepository<T>
    where T : IEntity
{
    private readonly IMongoDataAccessContext _context = context;
    private readonly IMongoCollection<T> _collection = collection;

    public async Task<T?> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        try
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
        catch (FormatException)
        {
            return default;
        }
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        try
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
        catch (FormatException)
        {
            return [];
        }
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        try
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
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.Revision = 1;
        await TryCatchDuplicate(async () =>
            {
                if (_context.Session is not null)
                {
                    await _collection
                        .InsertOneAsync(_context.Session, entity, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _collection
                        .InsertOneAsync(entity, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
            })
            .ConfigureAwait(false);
    }

    public async Task InsertAllAsync(IReadOnlyCollection<T> entities, CancellationToken cancellationToken = default)
    {
        foreach (T entity in entities)
            entity.Revision = 1;

        await TryCatchDuplicate(async () =>
            {
                if (_context.Session is not null)
                {
                    await _collection
                        .InsertManyAsync(_context.Session, entities, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _collection
                        .InsertManyAsync(entities, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
            })
            .ConfigureAwait(false);
    }

    public async Task<T?> UpdateAsync(
        Expression<Func<T, bool>> filter,
        Action<IUpdateBuilder<T>> update,
        bool upsert = false,
        bool returnOriginal = false,
        CancellationToken cancellationToken = default
    )
    {
        var updateBuilder = new MongoUpdateBuilder<T>();
        update(updateBuilder);
        updateBuilder.Inc(e => e.Revision, 1);
        UpdateDefinition<T> updateDef = updateBuilder.Build();
        var options = new FindOneAndUpdateOptions<T>
        {
            IsUpsert = upsert,
            ReturnDocument = returnOriginal ? ReturnDocument.Before : ReturnDocument.After
        };
        T? entity;
        try
        {
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
        }
        catch (FormatException)
        {
            return default;
        }
        return entity;
    }

    public async Task<int> UpdateAllAsync<TFilter>(
        Expression<Func<T, bool>> filter,
        Action<IUpdateBuilder<T>> update,
        string jsonArrayFilterDefinition,
        CancellationToken cancellationToken = default
    )
    {
        var updateOptions = new UpdateOptions
        {
            ArrayFilters = [new JsonArrayFilterDefinition<TFilter>(jsonArrayFilterDefinition)]
        };
        return await UpdateAllAsync(filter, update, updateOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> UpdateAllAsync(
        Expression<Func<T, bool>> filter,
        Action<IUpdateBuilder<T>> update,
        UpdateOptions? updateOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        var updateBuilder = new MongoUpdateBuilder<T>();
        update(updateBuilder);
        updateBuilder.Inc(e => e.Revision, 1);
        UpdateDefinition<T> updateDef = updateBuilder.Build();
        UpdateResult result;
        try
        {
            if (_context.Session is not null)
            {
                result = await _collection
                    .UpdateManyAsync(
                        _context.Session,
                        filter,
                        updateDef,
                        updateOptions,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            else
            {
                result = await _collection
                    .UpdateManyAsync(filter, updateDef, updateOptions, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (FormatException)
        {
            return 0;
        }
        return (int)result.ModifiedCount;
    }

    public async Task<T?> DeleteAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        try
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
        catch (FormatException)
        {
            return default;
        }
    }

    public async Task<int> DeleteAllAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        DeleteResult result;
        try
        {
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
        }
        catch (FormatException)
        {
            return 0;
        }
        return (int)result.DeletedCount;
    }

    public async Task<ISubscription<T>> SubscribeAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        var filterDef = new ExpressionFilterDefinition<T>(filter);
        BsonDocument renderedFilter = filterDef.Render(
            _collection.DocumentSerializer,
            _collection.Settings.SerializerRegistry,
            LinqProvider.V2
        );
        var findCommand = new BsonDocument
        {
            { "find", _collection.CollectionNamespace.CollectionName },
            { "filter", renderedFilter },
            { "limit", 1 },
            { "singleBatch", true }
        };
        BsonDocument result;
        if (_context.Session is not null)
        {
            result = await _collection
                .Database.RunCommandAsync<BsonDocument>(
                    _context.Session,
                    findCommand,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            result = await _collection
                .Database.RunCommandAsync<BsonDocument>(findCommand, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        BsonDocument? initialEntityDoc = result["cursor"]["firstBatch"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        T? initialEntity = initialEntityDoc is null ? default : BsonSerializer.Deserialize<T>(initialEntityDoc);
        var timestamp = (BsonTimestamp)result["operationTime"];
        var subscription = new MongoSubscription<T>(_context, _collection, filter.Compile(), timestamp, initialEntity);
        return subscription;
    }

    private static async Task TryCatchDuplicate(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (MongoCommandException e)
        {
            if (e.CodeName == "DuplicateKey")
                throw new DuplicateKeyException();
            throw;
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
                throw new DuplicateKeyException();
            throw;
        }
    }
}
