namespace SIL.DataAccess;

public interface IDataAccessContext : IDisposable
{
    Task<TResult> WithTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> callbackAsync,
        CancellationToken cancellationToken = default
    );
    Task WithTransactionAsync(
        Func<CancellationToken, Task> callbackAsync,
        CancellationToken cancellationToken = default
    );
}
