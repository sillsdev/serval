namespace SIL.DataAccess;

public interface ISubscription<T> : IDisposable
    where T : IEntity
{
    EntityChange<T> Change { get; }
    Task WaitForChangeAsync(
        TimeSpan? timeout = null,
        EntityChangeType[]? changeTypes = null,
        CancellationToken cancellationToken = default
    );
}
