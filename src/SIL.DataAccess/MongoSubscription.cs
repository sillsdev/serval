namespace SIL.DataAccess;

public class MongoSubscription<T> : DisposableBase, ISubscription<T>
    where T : IEntity
{
    private readonly IMongoDataAccessContext _context;
    private readonly IMongoCollection<T> _entities;
    private BsonTimestamp _timestamp;
    private readonly Func<T, bool> _filter;

    public MongoSubscription(
        IMongoDataAccessContext context,
        IMongoCollection<T> entities,
        Func<T, bool> filter,
        BsonTimestamp timestamp,
        T? initialEntity
    )
    {
        _context = context;
        _entities = entities;
        _filter = filter;
        _timestamp = timestamp;
        Change = new EntityChange<T>(
            initialEntity == null ? EntityChangeType.Delete : EntityChangeType.Update,
            initialEntity
        );
    }

    public EntityChange<T> Change { get; private set; }

    public async Task WaitForChangeAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        Expression<Func<ChangeStreamDocument<T>, bool>> changeEventFilter;
        if (Change.Entity is null)
            changeEventFilter = ce => ce.OperationType == ChangeStreamOperationType.Insert;
        else
            changeEventFilter = ce =>
                ce.DocumentKey["_id"] == new ObjectId(Change.Entity.Id)
                && (
                    ce.OperationType == ChangeStreamOperationType.Delete
                    || ce.FullDocument.Revision > Change.Entity.Revision
                );
        var options = new ChangeStreamOptions
        {
            FullDocument = ChangeStreamFullDocumentOption.UpdateLookup,
            MaxAwaitTime = timeout,
            StartAtOperationTime = _timestamp
        };
        PipelineDefinition<ChangeStreamDocument<T>, ChangeStreamDocument<T>> pipelineDef = PipelineDefinitionBuilder
            .For<ChangeStreamDocument<T>>()
            .Match(changeEventFilter);
        IChangeStreamCursor<ChangeStreamDocument<T>> cursor;
        if (_context.Session is not null)
        {
            cursor = await _entities
                .WatchAsync(_context.Session, pipelineDef, options, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            cursor = await _entities.WatchAsync(pipelineDef, options, cancellationToken).ConfigureAwait(false);
        }
        try
        {
            DateTime started = DateTime.UtcNow;

            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                bool entityNotFound = Change.Entity is null;
                bool changed = false;
                foreach (ChangeStreamDocument<T> ce in cursor.Current)
                {
                    EntityChangeType changeType = EntityChangeType.None;
                    switch (ce.OperationType)
                    {
                        case ChangeStreamOperationType.Insert:
                            changeType = EntityChangeType.Insert;
                            break;

                        case ChangeStreamOperationType.Replace:
                        case ChangeStreamOperationType.Update:
                            changeType = EntityChangeType.Update;
                            break;

                        case ChangeStreamOperationType.Delete:
                            changeType = EntityChangeType.Delete;
                            break;
                    }

                    if (entityNotFound)
                    {
                        if (ce.FullDocument is not null && _filter(ce.FullDocument))
                        {
                            Change = new EntityChange<T>(changeType, ce.FullDocument);
                            changed = true;
                        }
                    }
                    else
                    {
                        Change = new EntityChange<T>(changeType, ce.FullDocument);
                        changed = true;
                    }

                    _timestamp = ce.ClusterTime;
                }

                if (changed)
                    return;

                if (timeout.HasValue && DateTime.UtcNow - started >= timeout)
                    return;
            }
        }
        finally
        {
            cursor.Dispose();
        }
    }
}
