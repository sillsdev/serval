namespace SIL.DataAccess;

public interface ISubscription<T> : IDisposable
    where T : IEntity
{
    EntityChange<T> Change { get; }
    Task WaitForChangeAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    DateTime Timestamp { get; }
}
