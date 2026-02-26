namespace SIL.DataAccess;

public interface ISubscription<T> : IDisposable
    where T : IEntity
{
    EntityChange<T> Change { get; }
    Task WaitForChangeAsync(
        TimeSpan? timeout = default,
        bool insertsOrUpdatesOnly = false,
        CancellationToken cancellationToken = default
    );
}
