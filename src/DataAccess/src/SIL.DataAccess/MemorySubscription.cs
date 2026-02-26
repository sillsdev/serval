namespace SIL.DataAccess;

public class MemorySubscription<T>(T? initialEntity, Action<MemorySubscription<T>> remove)
    : DisposableBase,
        ISubscription<T>
    where T : IEntity
{
    private readonly Action<MemorySubscription<T>> _remove = remove;
    private readonly AsyncAutoResetEvent _changeEvent = new();

    public EntityChange<T> Change { get; private set; } =
        new EntityChange<T>(initialEntity == null ? EntityChangeType.Delete : EntityChangeType.Update, initialEntity);

    public async Task WaitForChangeAsync(
        TimeSpan? timeout = default,
        bool insertsOrUpdatesOnly = false,
        CancellationToken cancellationToken = default
    )
    {
        timeout ??= Timeout.InfiniteTimeSpan;
        await TaskTimeout(_changeEvent.WaitAsync, timeout.Value, cancellationToken).ConfigureAwait(false);
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
            await action(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task task = action(cts.Token);
            Task completedTask = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
            if (task != completedTask)
                cts.Cancel();
            await completedTask.ConfigureAwait(false);
        }
    }
}
