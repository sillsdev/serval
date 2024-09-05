namespace Serval.Machine.Shared.Services;

public interface IDistributedReaderWriterLock
{
    Task<IAsyncDisposable> ReaderLockAsync(
        TimeSpan? lifetime = default,
        TimeSpan? timeout = default,
        CancellationToken cancellationToken = default
    );
    Task<IAsyncDisposable> WriterLockAsync(
        TimeSpan? lifetime = default,
        TimeSpan? timeout = default,
        CancellationToken cancellationToken = default
    );
}
