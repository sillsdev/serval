namespace SIL.DataAccess;

public class MongoSubscription<T> : ObjectModel.DisposableBase, ISubscription<T>
    where T : IEntity
{
    private readonly IMongoDataAccessContext _context;
    private readonly IMongoCollection<T> _entities;
    private BsonTimestamp _timestamp;
    private readonly Func<T, bool> _filter;
    private IChangeStreamCursor<ChangeStreamDocument<T>>? _cursor;
    private readonly Expression<Func<ChangeStreamDocument<T>, bool>> _changeEventFilter;
    private TimeSpan? _timeout;
    private BsonDocument? _resumeToken;
    private readonly SubscriptionMode _mode;
    private readonly Queue<EntityChange<T>> _changes;
    public EntityChange<T> Change { get; private set; }

    public MongoSubscription(
        IMongoDataAccessContext context,
        IMongoCollection<T> entities,
        Func<T, bool> filter,
        BsonTimestamp timestamp,
        T? initialEntity,
        SubscriptionMode mode = SubscriptionMode.Repository
    )
    {
        _context = context;
        _entities = entities;
        _timestamp = timestamp;
        _filter = filter;
        _mode = mode;
        _changes = [];
        Change = new EntityChange<T>(
            initialEntity == null ? EntityChangeType.Delete : EntityChangeType.Update,
            initialEntity
        );
        Expression<Func<ChangeStreamDocument<T>, bool>> changeEventFilter;
        if (_mode == SubscriptionMode.Repository)
        {
            changeEventFilter = ce =>
                new HashSet<ChangeStreamOperationType>
                {
                    ChangeStreamOperationType.Insert,
                    ChangeStreamOperationType.Update,
                    ChangeStreamOperationType.Replace,
                }.Contains(ce.OperationType);
        }
        else if (Change.Entity is null)
        {
            changeEventFilter = ce => ce.OperationType == ChangeStreamOperationType.Insert;
        }
        else
        {
            changeEventFilter = ce =>
                ce.DocumentKey["_id"] == new ObjectId(Change.Entity.Id)
                && (
                    ce.OperationType == ChangeStreamOperationType.Delete
                    || ce.FullDocument.Revision > Change.Entity.Revision
                );
        }
        _changeEventFilter = changeEventFilter;
    }

    public async Task WaitForChangeAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_mode == SubscriptionMode.Repository && _changes.TryDequeue(out EntityChange<T> change))
        {
            Change = change;
            return;
        }

        if (_cursor is null || !timeout.Equals(_timeout))
        {
            _cursor?.Dispose();
            _timeout = timeout;
            var options = new ChangeStreamOptions
            {
                FullDocument = ChangeStreamFullDocumentOption.UpdateLookup,
                MaxAwaitTime = _timeout,
            };
            if (_resumeToken is not null)
            {
                options.ResumeAfter = _resumeToken;
            }
            else
            {
                options.StartAtOperationTime = _timestamp;
            }
            PipelineDefinition<ChangeStreamDocument<T>, ChangeStreamDocument<T>> pipelineDef = PipelineDefinitionBuilder
                .For<ChangeStreamDocument<T>>()
                .Match(_changeEventFilter);
            if (_context.Session is not null)
            {
                _cursor = await _entities
                    .WatchAsync(_context.Session, pipelineDef, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                _cursor = await _entities.WatchAsync(pipelineDef, options, cancellationToken).ConfigureAwait(false);
            }
        }

        DateTime started = DateTime.UtcNow;

        try
        {
            while (await _cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                bool entityNotFound = Change.Entity is null;
                bool changed = false;
                foreach (ChangeStreamDocument<T> ce in _cursor.Current)
                {
                    _timestamp = ce.ClusterTime;
                    _resumeToken = ce.ResumeToken;

                    if (
                        ce.FullDocument is not null
                        && Change.Entity is not null
                        && ce.FullDocument.Equals(Change.Entity)
                    )
                    {
                        continue;
                    }

                    EntityChangeType changeType = ce.OperationType switch
                    {
                        ChangeStreamOperationType.Insert => EntityChangeType.Insert,
                        ChangeStreamOperationType.Replace or ChangeStreamOperationType.Update =>
                            EntityChangeType.Update,
                        ChangeStreamOperationType.Delete => EntityChangeType.Delete,
                        _ => EntityChangeType.None,
                    };

                    if (_mode == SubscriptionMode.Repository)
                    {
                        if (ce.FullDocument is not null && _filter(ce.FullDocument))
                            _changes.Enqueue(new EntityChange<T>(changeType, ce.FullDocument));
                    }
                    else if (entityNotFound)
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
                }

                if (_mode == SubscriptionMode.Repository && _changes.TryDequeue(out change))
                {
                    Change = change;
                    return;
                }

                if (changed)
                    return;

                if (timeout.HasValue && DateTime.UtcNow - started >= timeout)
                    return;
            }
        }
        catch (MongoException)
        {
            _cursor?.Dispose();
            _cursor = null;
            throw;
        }
    }

    protected override void DisposeManagedResources()
    {
        _cursor?.Dispose();
    }
}
