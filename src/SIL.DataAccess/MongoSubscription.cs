namespace SIL.DataAccess;

public class MongoSubscription<T> : ISubscription<T>
    where T : IEntity
{
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IMongoCollection<T> _entities;
    private BsonTimestamp _timestamp;
    private readonly Func<T, bool> _filter;
    private bool disposedValue;

    public MongoSubscription(IMongoCollection<T> entities, Func<T, bool> filter, DateTime currentTime, T? initialEntity)
    {
        _entities = entities;
        _filter = filter;
        _timestamp = new BsonTimestamp(Convert.ToInt32((currentTime - Epoch).TotalSeconds), 1);
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
        using IChangeStreamCursor<ChangeStreamDocument<T>> cursor = await _entities.WatchAsync(
            PipelineDefinitionBuilder.For<ChangeStreamDocument<T>>().Match(changeEventFilter),
            options,
            cancellationToken
        );
        DateTime started = DateTime.UtcNow;

        while (await cursor.MoveNextAsync(cancellationToken))
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

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
