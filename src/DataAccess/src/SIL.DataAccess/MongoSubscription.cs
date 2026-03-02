namespace SIL.DataAccess;

public class MongoSubscription<T>(
    IMongoDataAccessContext context,
    IMongoCollection<T> entities,
    Func<T, bool> filter,
    BsonTimestamp timestamp,
    T? initialEntity
) : DisposableBase, ISubscription<T>
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
        EntityChangeType[]? changeTypes = null,
        CancellationToken cancellationToken = default
    )
    {
        Expression<Func<ChangeStreamDocument<T>, bool>> changeEventFilter;
        if (changeTypes is not null && changeTypes.Length > 0)
        {
            changeEventFilter = BuildChangeEventFilter(changeTypes);
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

    private static Expression<Func<ChangeStreamDocument<T>, bool>> BuildChangeEventFilter(
        EntityChangeType[] changeTypes
    )
    {
        HashSet<ChangeStreamOperationType> changeStreamOperations = [];
        foreach (EntityChangeType ct in changeTypes)
        {
            switch (ct)
            {
                case EntityChangeType.Insert:
                    changeStreamOperations.Add(ChangeStreamOperationType.Insert);
                    break;
                case EntityChangeType.Update:
                    changeStreamOperations.Add(ChangeStreamOperationType.Update);
                    changeStreamOperations.Add(ChangeStreamOperationType.Replace);
                    break;
                case EntityChangeType.Delete:
                    changeStreamOperations.Add(ChangeStreamOperationType.Delete);
                    break;
                case EntityChangeType.None:
                default:
                    break;
            }
        }

        if (changeStreamOperations.Count == 0)
            throw new ArgumentException("No valid change types specified.", nameof(changeTypes));

        // Create an expression matching:
        // ce => ce.OperationType == ChangeStreamOperationType.Insert || ce.OperationType == ChangeStreamOperationType.Update
        ParameterExpression ce = Expression.Parameter(typeof(ChangeStreamDocument<T>), "ce");
        MemberExpression opMember = Expression.Property(ce, nameof(ChangeStreamDocument<>.OperationType));

        Expression changeEventFilter = Expression.Constant(false);
        changeEventFilter = changeStreamOperations.Aggregate(
            changeEventFilter,
            (current, op) => Expression.OrElse(current, Expression.Equal(opMember, Expression.Constant(op)))
        );
        return Expression.Lambda<Func<ChangeStreamDocument<T>, bool>>(changeEventFilter, ce);
    }
}
