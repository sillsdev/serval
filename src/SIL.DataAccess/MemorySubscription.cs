namespace SIL.DataAccess;

public class MemorySubscription<T> : DisposableBase, ISubscription<T>
    where T : IEntity
{
    private readonly Action<MemorySubscription<T>> _remove;
    private readonly AsyncAutoResetEvent _changeEvent;

    public MemorySubscription(T? initialEntity, Action<MemorySubscription<T>> remove)
    {
        _remove = remove;
        _changeEvent = new AsyncAutoResetEvent();
        Change = new EntityChange<T>(
            initialEntity == null ? EntityChangeType.Delete : EntityChangeType.Update,
            initialEntity
        );
    }

    public EntityChange<T> Change { get; private set; }

    public async Task WaitForChangeAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        if (timeout is null)
            timeout = Timeout.InfiniteTimeSpan;
        await TaskTimeout(ct => _changeEvent.WaitAsync(ct), timeout.Value, cancellationToken);
    }

    internal void HandleChange(EntityChange<T> change)
    {
        Change = change;
        _changeEvent.Set();
    }

    protected override void DisposeManagedResources()
    {
        _remove(this);
    }

    private static async Task TaskTimeout(
        Func<CancellationToken, Task> action,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await action(cancellationToken);
        }
        else
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task task = action(cts.Token);
            Task completedTask = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken));
            if (task != completedTask)
                cts.Cancel();
            await completedTask;
        }
    }
}
