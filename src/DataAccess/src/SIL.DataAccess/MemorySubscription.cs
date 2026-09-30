using System.Collections.Concurrent;

namespace SIL.DataAccess;

public class MemorySubscription<T>(T? initialEntity, Action<MemorySubscription<T>> remove, SubscriptionMode mode)
    : ObjectModel.DisposableBase,
        ISubscription<T>
    where T : IEntity
{
    private readonly Action<MemorySubscription<T>> _remove = remove;
    private readonly AsyncAutoResetEvent _changeEvent = new(false);
    private readonly ConcurrentQueue<EntityChange<T>> _changes = new();
    public SubscriptionMode Mode { get; } = mode;

    public EntityChange<T> Change { get; private set; } =
        new EntityChange<T>(initialEntity == null ? EntityChangeType.Delete : EntityChangeType.Update, initialEntity);

    public async Task WaitForChangeAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (Mode == SubscriptionMode.Repository)
        {
            await TaskTimeout(_changeEvent.WaitAsync, timeout ?? Timeout.InfiniteTimeSpan, cancellationToken)
                .ConfigureAwait(false);

            if (_changes.TryDequeue(out EntityChange<T> change))
                Change = change;

            if (!_changes.IsEmpty)
                _changeEvent.Set();
        }
        else
        {
            await TaskTimeout(_changeEvent.WaitAsync, timeout ?? Timeout.InfiniteTimeSpan, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal void HandleChange(EntityChange<T> change)
    {
        if (Mode == SubscriptionMode.Repository)
        {
            _changes.Enqueue(change);
        }
        else
        {
            Change = change;
        }
        _changeEvent.Set();
    }

    protected override void DisposeManagedResources()
    {
        _remove(this);
    }

    private static async Task TaskTimeout(
        Func<CancellationToken, ValueTask> action,
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
            Task task = action(cts.Token).AsTask();
            Task completedTask = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
            if (task != completedTask)
                cts.Cancel();
            await completedTask.ConfigureAwait(false);
        }
    }
}
