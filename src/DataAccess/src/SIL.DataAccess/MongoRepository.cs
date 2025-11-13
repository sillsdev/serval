namespace SIL.DataAccess;

public class MongoRepository<T>(IMongoDataAccessContext context, IMongoCollection<T> collection) : IRepository<T>
    where T : IEntity
{
    private readonly IMongoDataAccessContext _context = context;

    internal IMongoCollection<T> Collection { get; } = collection;

    public async Task<T?> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_context.Session is not null)
            {
                return await Collection
                    .AsQueryable(_context.Session)
                    .FirstOrDefaultAsync(filter, cancellationToken)
                    .ConfigureAwait(false);
            }
            return await Collection.AsQueryable().FirstOrDefaultAsync(filter, cancellationToken).ConfigureAwait(false);
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
                return await Collection
                    .AsQueryable(_context.Session)
                    .Where(filter)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            return await Collection.AsQueryable().Where(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<T>> GetAllWithJoinAsync<T2, TKey>(
        Expression<Func<T, bool>> thisFilter,
        Expression<Func<T2, bool>> otherFilter,
        IRepository<T2> otherRepository,
        Expression<Func<T, TKey>> thisKey,
        Expression<Func<T2, TKey>> otherKey,
        CancellationToken cancellationToken = default
    )
        where T2 : IEntity
    {
        try
        {
            if (otherRepository is not MongoRepository<T2> otherMongoRepository)
                throw new NotSupportedException();

            // Note: The MongoDB driver does not perform a Lookup pipeline if it contains a Where(),
            // and if a Where() with an Any() is placed after the Lookup(), you cannot pass an
            // Expression<Func<T, TKey>> (as the Results are not IQueryable), and if you pass a Func<T, TKey>,
            // it complains you have passed a constant expression, not a lambda expression. This appears
            // to be an idiosyncracy of LINQ3, as manually typing an expression into the Any() works fine.
            // Thus, we retrieve the Lookup results into memory and perform the otherFilter there.
            if (_context.Session is not null)
            {
                return
                [
                    .. (
                    await Collection
                        .AsQueryable(_context.Session)
                        .Where(thisFilter)
                        .Lookup(otherMongoRepository.Collection, thisKey, otherKey)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false)
                )
                    .Where(t => t.Results.AsQueryable().Any(otherFilter))
                    .Select(t => t.Local)
                ];
            }

            return
            [
                .. (
                    await Collection
                        .AsQueryable()
                        .Where(thisFilter)
                        .Lookup(otherMongoRepository.Collection, thisKey, otherKey)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false)
                )
                    .Where(t => t.Results.AsQueryable().Any(otherFilter))
                    .Select(t => t.Local)
            ];
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
                return await Collection
                    .AsQueryable(_context.Session)
                    .AnyAsync(filter, cancellationToken)
                    .ConfigureAwait(false);
            }
            return await Collection.AsQueryable().AnyAsync(filter, cancellationToken).ConfigureAwait(false);
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
                    await Collection
                        .InsertOneAsync(_context.Session, entity, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken).ConfigureAwait(false);
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
                    await Collection
                        .InsertManyAsync(_context.Session, entities, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await Collection
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
        (UpdateDefinition<T> updateDef, IReadOnlyList<ArrayFilterDefinition> arrayFilters) = updateBuilder.Build();
        var options = new FindOneAndUpdateOptions<T>
        {
            IsUpsert = upsert,
            ReturnDocument = returnOriginal ? ReturnDocument.Before : ReturnDocument.After
        };
        if (arrayFilters.Count > 0)
            options.ArrayFilters = arrayFilters;
        T? entity;
        try
        {
            if (_context.Session is not null)
            {
                entity = await Collection
                    .FindOneAndUpdateAsync(_context.Session, filter, updateDef, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                entity = await Collection
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

    public async Task<int> UpdateAllAsync(
        Expression<Func<T, bool>> filter,
        Action<IUpdateBuilder<T>> update,
        CancellationToken cancellationToken = default
    )
    {
        var updateBuilder = new MongoUpdateBuilder<T>();
        update(updateBuilder);
        updateBuilder.Inc(e => e.Revision, 1);
        (UpdateDefinition<T> updateDef, IReadOnlyList<ArrayFilterDefinition> arrayFilters) = updateBuilder.Build();
        UpdateOptions? updateOptions = null;
        if (arrayFilters.Count > 0)
            updateOptions = new UpdateOptions { ArrayFilters = arrayFilters };
        UpdateResult result;
        try
        {
            if (_context.Session is not null)
            {
                result = await Collection
                    .UpdateManyAsync(_context.Session, filter, updateDef, updateOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                result = await Collection
                    .UpdateManyAsync(filter, updateDef, updateOptions, cancellationToken)
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
                return await Collection
                    .FindOneAndDeleteAsync(_context.Session, filter, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            return await Collection
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
                result = await Collection
                    .DeleteManyAsync(_context.Session, filter, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                result = await Collection
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
            new RenderArgs<T>(Collection.DocumentSerializer, Collection.Settings.SerializerRegistry)
        );
        var findCommand = new BsonDocument
        {
            { "find", Collection.CollectionNamespace.CollectionName },
            { "filter", renderedFilter },
            { "limit", 1 },
            { "singleBatch", true }
        };
        BsonDocument result;
        if (_context.Session is not null)
        {
            result = await Collection
                .Database.RunCommandAsync<BsonDocument>(
                    _context.Session,
                    findCommand,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            result = await Collection
                .Database.RunCommandAsync<BsonDocument>(findCommand, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        BsonDocument? initialEntityDoc = result["cursor"]["firstBatch"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        T? initialEntity = initialEntityDoc is null ? default : BsonSerializer.Deserialize<T>(initialEntityDoc);
        var timestamp = (BsonTimestamp)result["operationTime"];
        var subscription = new MongoSubscription<T>(_context, Collection, filter.Compile(), timestamp, initialEntity);
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
