namespace SIL.DataAccess;

public class MemoryDataAccessContext : DisposableBase, IDataAccessContext
{
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task AbortTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
