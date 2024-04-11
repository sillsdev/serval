namespace SIL.DataAccess;

public class MemoryDataAccessContext : DisposableBase, IDataAccessContext
{
    public async Task<TResult> WithTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> callbackAsync,
        CancellationToken cancellationToken = default
    )
    {
        return await callbackAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task WithTransactionAsync(
        Func<CancellationToken, Task> callbackAsync,
        CancellationToken cancellationToken = default
    )
    {
        await callbackAsync(cancellationToken).ConfigureAwait(false);
    }
}
