namespace EchoEngine;

using System.Collections.Concurrent;

public class BackgroundTaskQueue
{
    private const int Capacity = 128;
    private readonly ConcurrentDictionary<string, (string, CancellationTokenSource)> _activeBuilds = new();
    private readonly Channel<Func<IServiceProvider, CancellationToken, ValueTask>> _queue;

    /// <summary>
    /// A concurrent dictionary to keep track of active builds, where engine id is the key,
    /// and the value is a tuple of the build id and the cancellation token source.
    /// </summary>
    public ConcurrentDictionary<string, (string, CancellationTokenSource)> ActiveBuilds => _activeBuilds;

    public BackgroundTaskQueue()
    {
        // Capacity should be set based on the expected application load and
        // number of concurrent threads accessing the queue.
        // BoundedChannelFullMode.Wait will cause calls to WriteAsync() to return a task,
        // which completes only when space became available. This leads to backpressure,
        // in case too many publishers/calls start accumulating.
        var options = new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait };
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, ValueTask>>(options);
    }

    public async ValueTask QueueBackgroundWorkItemAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(
        CancellationToken cancellationToken
    )
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
