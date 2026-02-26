namespace SIL.DataAccess;

public class MongoSubscription<T>(
    IMongoDataAccessContext context,
    IMongoCollection<T> entities,
    Func<T, bool> filter,
    BsonTimestamp timestamp,
    T? initialEntity
) : ObjectModel.DisposableBase, ISubscription<T>
    where T : IEntity
{
    private readonly IMongoDataAccessContext _context = context;
    private readonly IMongoCollection<T> _entities = entities;
    private BsonTimestamp _timestamp = timestamp;
    private readonly Func<T, bool> _filter = filter;

    public EntityChange<T> Change { get; private set; } =
        new EntityChange<T>(initialEntity == null ? EntityChangeType.Delete : EntityChangeType.Update, initialEntity);

    public async Task WaitForChangeAsync(
        TimeSpan? timeout = null,
        IReadOnlySet<EntityChangeType>? changeTypes = null,
        CancellationToken cancellationToken = default
    )
    {
        Expression<Func<ChangeStreamDocument<T>, bool>> changeEventFilter;
        if (changeTypes is not null && changeTypes.Count > 0)
        {
            HashSet<ChangeStreamOperationType> ops =
            [
                .. changeTypes.SelectMany<EntityChangeType, ChangeStreamOperationType>(ct =>
                    ct switch
                    {
                        EntityChangeType.Insert => [ChangeStreamOperationType.Insert],
                        EntityChangeType.Update =>
                        [
                            ChangeStreamOperationType.Update,
                            ChangeStreamOperationType.Replace,
                        ],
                        EntityChangeType.Delete => [ChangeStreamOperationType.Delete],
                        _ => throw new ArgumentException("No valid change types specified.", nameof(changeTypes)),
                    }
                ),
            ];
            changeEventFilter = ce => ops.Contains(ce.OperationType);
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
        var options = new ChangeStreamOptions
        {
            FullDocument = ChangeStreamFullDocumentOption.UpdateLookup,
            MaxAwaitTime = timeout,
            StartAtOperationTime = _timestamp,
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
                    EntityChangeType changeType = ce.OperationType switch
                    {
                        ChangeStreamOperationType.Insert => EntityChangeType.Insert,
                        ChangeStreamOperationType.Replace or ChangeStreamOperationType.Update =>
                            EntityChangeType.Update,
                        ChangeStreamOperationType.Delete => EntityChangeType.Delete,
                        _ => EntityChangeType.None,
                    };

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
