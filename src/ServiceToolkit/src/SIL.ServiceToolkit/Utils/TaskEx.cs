namespace SIL.ServiceToolkit.Utils;

public static class TaskEx
{
    public static async Task<(bool, T?)> Timeout<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        if (timeout == System.Threading.Timeout.InfiniteTimeSpan)
            return (true, await action(cancellationToken));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<T> task = action(cts.Token);
        Task<T?> completedTask = await Task.WhenAny(task as Task<T?>, Delay<T>(timeout, cancellationToken));
        T? result = await completedTask;
        if (completedTask == task)
            return (true, result);

        cts.Cancel();
        return (false, result);
    }

    public static async Task<bool> Timeout(
        Func<CancellationToken, Task> action,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        if (timeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            await action(cancellationToken);
            return true;
        }
        else
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task task = action(cts.Token);
            Task completedTask = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken));
            await completedTask;
            if (completedTask == task)
                return true;

            cts.Cancel();
            return false;
        }
    }

    private static async Task<T?> Delay<T>(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        await Task.Delay(timeout, cancellationToken);
        return default;
    }
}
