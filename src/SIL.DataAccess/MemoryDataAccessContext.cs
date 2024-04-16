namespace SIL.DataAccess;

public class MemoryDataAccessContext : DisposableBase, IDataAccessContext
{
    public Task<TResult> WithTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> callbackAsync,
        CancellationToken cancellationToken = default
    )
    {
        return callbackAsync(cancellationToken);
    }

    public Task WithTransactionAsync(
        Func<CancellationToken, Task> callbackAsync,
        CancellationToken cancellationToken = default
    )
    {
        return callbackAsync(cancellationToken);
    }
}
