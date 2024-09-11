namespace Serval.Machine.Shared.Services;

[TestFixture]
public class DistributedReaderWriterLockTests
{
    [Test]
    public async Task ReaderLockAsync_NoLockAcquired()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        await rwLock.ReaderLockAsync(ct =>
        {
            RWLock lockEntity = env.Locks.Get("test");
            Assert.That(lockEntity.IsAvailableForReading(), Is.True);
            Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
            return Task.CompletedTask;
        });

        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    [Test]
    public async Task ReaderLockAsync_ReaderLockAcquired()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        await rwLock.ReaderLockAsync(async ct =>
        {
            await rwLock.ReaderLockAsync(
                ct =>
                {
                    RWLock lockEntity = env.Locks.Get("test");
                    Assert.That(lockEntity.IsAvailableForReading(), Is.True);
                    Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
                    return Task.CompletedTask;
                },
                cancellationToken: ct
            );
        });

        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    [Test]
    public async Task ReaderLockAsync_WriterLockAcquiredAndNotReleased()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        using CancellationTokenSource cts = new();
        Task task1 = rwLock.WriterLockAsync(
            ct => Task.Delay(Timeout.InfiniteTimeSpan, ct),
            cancellationToken: cts.Token
        );
        Task task2 = rwLock.ReaderLockAsync(
            ct => Task.Delay(Timeout.InfiniteTimeSpan, ct),
            cancellationToken: cts.Token
        );

        await AssertNeverCompletesAsync(task2);

        cts.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await task1);
        Assert.ThrowsAsync<TaskCanceledException>(async () => await task2);
    }

    [Test]
    public async Task ReaderLockAsync_WriterLockAcquiredAndReleased()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        AsyncManualResetEvent @event = new(false);
        Task<Task> outerTask = rwLock.WriterLockAsync(async ct =>
        {
            Task task = rwLock.ReaderLockAsync(
                ct =>
                {
                    RWLock lockEntity = env.Locks.Get("test");
                    Assert.That(lockEntity.IsAvailableForReading(), Is.True);
                    Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
                    return Task.CompletedTask;
                },
                cancellationToken: ct
            );
            Assert.That(task.IsCompleted, Is.False);
            await @event.WaitAsync(ct);
            return task;
        });

        @event.Set();
        Task innerTask = await outerTask;
        await innerTask;
        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    [Test]
    public async Task ReaderLockAsync_WriterLockAcquiredAndExpired()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        Task? innerTask = null;
        Task outerTask = rwLock.WriterLockAsync(
            async ct =>
            {
                innerTask = rwLock.ReaderLockAsync(
                    ct =>
                    {
                        RWLock lockEntity = env.Locks.Get("test");
                        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
                        Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
                        return Task.CompletedTask;
                    },
                    cancellationToken: CancellationToken.None
                );
                await Task.Delay(500, ct);
            },
            lifetime: TimeSpan.FromMilliseconds(400)
        );

        Assert.ThrowsAsync<TimeoutException>(async () => await outerTask);
        Assert.That(innerTask, Is.Not.Null);
        await innerTask;
        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    [Test]
    public async Task ReaderLockAsync_Cancelled()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        await rwLock.WriterLockAsync(ct =>
        {
            using CancellationTokenSource cts = new();
            Task task = rwLock.ReaderLockAsync(
                ct => Task.Delay(Timeout.InfiniteTimeSpan, ct),
                cancellationToken: cts.Token
            );
            cts.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await task);
            return Task.CompletedTask;
        });

        await rwLock.ReaderLockAsync(ct =>
        {
            RWLock lockEntity = env.Locks.Get("test");
            Assert.That(lockEntity.IsAvailableForReading(), Is.True);
            Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
            return Task.CompletedTask;
        });

        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    [Test]
    public async Task WriterLockAsync_NoLockAcquired()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        await rwLock.WriterLockAsync(ct =>
        {
            RWLock lockEntity = env.Locks.Get("test");
            Assert.That(lockEntity.IsAvailableForReading(), Is.False);
            Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
            return Task.CompletedTask;
        });

        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    [Test]
    public async Task WriterLockAsync_ReaderLockAcquiredAndNotReleased()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        using CancellationTokenSource cts = new();
        Task task1 = rwLock.ReaderLockAsync(
            ct => Task.Delay(Timeout.InfiniteTimeSpan, ct),
            cancellationToken: cts.Token
        );
        Task task2 = rwLock.WriterLockAsync(
            ct => Task.Delay(Timeout.InfiniteTimeSpan, ct),
            cancellationToken: cts.Token
        );

        await AssertNeverCompletesAsync(task2);

        cts.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await task1);
        Assert.ThrowsAsync<TaskCanceledException>(async () => await task2);
    }

    [Test]
    public async Task WriterLockAsync_ReaderLockAcquiredAndReleased()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        AsyncManualResetEvent @event = new(false);
        Task<Task> outerTask = rwLock.ReaderLockAsync(async ct =>
        {
            Task task = rwLock.WriterLockAsync(
                ct =>
                {
                    RWLock lockEntity = env.Locks.Get("test");
                    Assert.That(lockEntity.IsAvailableForReading(), Is.False);
                    Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
                    return Task.CompletedTask;
                },
                cancellationToken: ct
            );
            Assert.That(task.IsCompleted, Is.False);
            await @event.WaitAsync(ct);
            return task;
        });

        @event.Set();
        Task innerTask = await outerTask;
        await innerTask;
        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    [Test]
    public async Task WriterLockAsync_WriterLockAcquiredAndNeverReleased()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        using CancellationTokenSource cts = new();
        Task task1 = rwLock.WriterLockAsync(
            ct => Task.Delay(Timeout.InfiniteTimeSpan, ct),
            cancellationToken: cts.Token
        );
        Task task2 = rwLock.WriterLockAsync(
            ct => Task.Delay(Timeout.InfiniteTimeSpan, ct),
            cancellationToken: cts.Token
        );

        await AssertNeverCompletesAsync(task2);

        cts.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await task1);
        Assert.ThrowsAsync<TaskCanceledException>(async () => await task2);
    }

    [Test]
    public async Task WriterLockAsync_WriterLockAcquiredAndReleased()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        AsyncManualResetEvent @event = new(false);
        Task<Task> outerTask = rwLock.WriterLockAsync(async ct =>
        {
            Task task = rwLock.WriterLockAsync(
                ct =>
                {
                    RWLock lockEntity = env.Locks.Get("test");
                    Assert.That(lockEntity.IsAvailableForReading(), Is.False);
                    Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
                    return Task.CompletedTask;
                },
                cancellationToken: ct
            );
            Assert.That(task.IsCompleted, Is.False);
            await @event.WaitAsync(ct);
            return task;
        });

        @event.Set();
        Task innerTask = await outerTask;
        await innerTask;
        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    [Test]
    public async Task WriterLockAsync_WriterLockTakesPriorityOverReaderLock()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        int value = 1;
        AsyncManualResetEvent @event = new(false);
        Task<(Task<int>, Task<int>)> outerTask = rwLock.WriterLockAsync(async ct =>
        {
            Task<int> readTask = rwLock.ReaderLockAsync(ct => Task.FromResult(value++), cancellationToken: ct);
            Assert.That(readTask.IsCompleted, Is.False);
            Task<int> writeTask = rwLock.WriterLockAsync(ct => Task.FromResult(value++), cancellationToken: ct);
            Assert.That(writeTask.IsCompleted, Is.False);
            await @event.WaitAsync(ct);
            return (writeTask, readTask);
        });

        @event.Set();
        (Task<int> writeTask, Task<int> readTask) = await outerTask;
        Assert.That(await writeTask, Is.EqualTo(1));
        Assert.That(await readTask, Is.EqualTo(2));
    }

    [Test]
    public async Task WriterLockAsync_FirstWriterLockHasPriority()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        int value = 1;
        AsyncManualResetEvent @event = new(false);
        Task<(Task<int>, Task<int>)> outerTask = rwLock.WriterLockAsync(async ct =>
        {
            Task<int> task1 = rwLock.WriterLockAsync(ct => Task.FromResult(value++), cancellationToken: ct);
            Assert.That(task1.IsCompleted, Is.False);
            Task<int> task2 = rwLock.WriterLockAsync(ct => Task.FromResult(value++), cancellationToken: ct);
            Assert.That(task2.IsCompleted, Is.False);
            await @event.WaitAsync(ct);
            return (task1, task2);
        });

        @event.Set();
        (Task<int> task1, Task<int> task2) = await outerTask;
        Assert.That(await task1, Is.EqualTo(1));
        Assert.That(await task2, Is.EqualTo(2));
    }

    [Test]
    public async Task WriterLockAsync_WriterLockAcquiredAndExpired()
    {
        TestEnvironment env = new();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        Task? innerTask = null;
        Task outerTask = rwLock.WriterLockAsync(
            async ct =>
            {
                innerTask = rwLock.WriterLockAsync(
                    ct =>
                    {
                        RWLock lockEntity = env.Locks.Get("test");
                        Assert.That(lockEntity.IsAvailableForReading(), Is.False);
                        Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
                        return Task.CompletedTask;
                    },
                    cancellationToken: CancellationToken.None
                );
                await Task.Delay(500, ct);
            },
            lifetime: TimeSpan.FromMilliseconds(400)
        );

        Assert.ThrowsAsync<TimeoutException>(async () => await outerTask);
        Assert.That(innerTask, Is.Not.Null);
        await innerTask;
        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    [Test]
    public async Task WriterLockAsync_Cancelled()
    {
        var env = new TestEnvironment();
        IDistributedReaderWriterLock rwLock = await env.Factory.CreateAsync("test");

        await rwLock.WriterLockAsync(ct =>
        {
            using CancellationTokenSource cts = new();
            Task task = rwLock.WriterLockAsync(
                ct => Task.Delay(Timeout.InfiniteTimeSpan, ct),
                cancellationToken: cts.Token
            );
            cts.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await task);
            return Task.CompletedTask;
        });

        RWLock lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);

        await rwLock.WriterLockAsync(ct =>
        {
            RWLock lockEntity = env.Locks.Get("test");
            Assert.That(lockEntity.IsAvailableForReading(), Is.False);
            Assert.That(lockEntity.IsAvailableForWriting(), Is.False);
            return Task.CompletedTask;
        });

        lockEntity = env.Locks.Get("test");
        Assert.That(lockEntity.IsAvailableForReading(), Is.True);
        Assert.That(lockEntity.IsAvailableForWriting(), Is.True);
    }

    private static async Task AssertNeverCompletesAsync(Task task, int timeout = 100)
    {
        if (task.IsCompleted)
            Assert.Fail("Task completed unexpectedly.");
        Task completedTask = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (completedTask == task)
            Assert.Fail("Task completed unexpectedly.");
        var _ = task.ContinueWith(
            t =>
            {
                if (!t.IsCanceled)
                    Assert.Fail("Task completed unexpectedly.");
            },
            TaskScheduler.Default
        );
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Locks = new MemoryRepository<RWLock>();
            var idGenerator = new ObjectIdGenerator();
            var serviceOptions = Substitute.For<IOptions<ServiceOptions>>();
            serviceOptions.Value.Returns(new ServiceOptions { ServiceId = "host" });
            var lockOptions = Substitute.For<IOptions<DistributedReaderWriterLockOptions>>();
            lockOptions.Value.Returns(new DistributedReaderWriterLockOptions());
            Factory = new DistributedReaderWriterLockFactory(serviceOptions, lockOptions, Locks, idGenerator);
        }

        public DistributedReaderWriterLockFactory Factory { get; }
        public MemoryRepository<RWLock> Locks { get; }
    }
}
