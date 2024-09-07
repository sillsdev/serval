namespace Serval.Machine.Shared.Services;

public interface IDistributedReaderWriterLock
{
    Task ReaderLockAsync(
        Func<CancellationToken, Task> action,
        TimeSpan? lifetime = default,
        CancellationToken cancellationToken = default
    );
    Task WriterLockAsync(
        Func<CancellationToken, Task> action,
        TimeSpan? lifetime = default,
        CancellationToken cancellationToken = default
    );

    Task<T> ReaderLockAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan? lifetime = default,
        CancellationToken cancellationToken = default
    );
    Task<T> WriterLockAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan? lifetime = default,
        CancellationToken cancellationToken = default
    );
}
