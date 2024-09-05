namespace Serval.Machine.Shared.Services;

public class DistributedReaderWriterLock(
    string hostId,
    IRepository<RWLock> locks,
    IIdGenerator idGenerator,
    string id,
    TimeoutOptions timeoutOptions
) : IDistributedReaderWriterLock
{
    private readonly string _hostId = hostId;
    private readonly IRepository<RWLock> _locks = locks;
    private readonly IIdGenerator _idGenerator = idGenerator;
    private readonly string _id = id;
    private readonly TimeoutOptions _timeoutOptions = timeoutOptions;

    public async Task<IAsyncDisposable> ReaderLockAsync(
        TimeSpan? lifetime = default,
        TimeSpan? timeout = default,
        CancellationToken cancellationToken = default
    )
    {
        string lockId = _idGenerator.GenerateId();
        DateTime timeoutExpires = DateTime.UtcNow + (timeout ?? _timeoutOptions.DefaultLockTimeout);
        await TaskEx.Timeout(
            async ct => await TryReaderLock(lockId, lifetime ?? _timeoutOptions.DefaultLockLifetime, ct),
            timeout ?? _timeoutOptions.DefaultLockTimeout,
            throwOnTimeout: true,
            cancellationToken
        );
        return new ReaderLockReleaser(this, lockId);
    }

    public async Task<IAsyncDisposable> WriterLockAsync(
        TimeSpan? lifetime = default,
        TimeSpan? timeout = default,
        CancellationToken cancellationToken = default
    )
    {
        string lockId = _idGenerator.GenerateId();
        StackTrace stackTrace = new StackTrace();
        DateTime timeoutExpires = DateTime.UtcNow + (timeout ?? _timeoutOptions.DefaultLockTimeout);

        await TaskEx.Timeout(
            async ct => await TryWriterLock(lockId, lifetime ?? _timeoutOptions.DefaultLockLifetime, ct),
            timeout ?? _timeoutOptions.DefaultLockTimeout,
            throwOnTimeout: true,
            cancellationToken
        );
        return new WriterLockReleaser(this, lockId);
    }

    private async Task TryWriterLock(string lockId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        if (!await TryAcquireWriterLock(lockId, lifetime, cancellationToken))
        {
            await _locks.UpdateAsync(
                _id,
                u => u.Add(rwl => rwl.WriterQueue, new Lock { Id = lockId, HostId = _hostId, }),
                cancellationToken: cancellationToken
            );
            try
            {
                using ISubscription<RWLock> sub = await _locks.SubscribeAsync(rwl => rwl.Id == _id, cancellationToken);
                do
                {
                    RWLock? rwLock = sub.Change.Entity;
                    if (rwLock is not null && !rwLock.IsAvailableForWriting(lockId))
                    {
                        var dateTimes = rwLock.ReaderLocks.Select(l => l.ExpiresAt).ToList();
                        if (rwLock.WriterLock?.ExpiresAt is not null)
                            dateTimes.Add(rwLock.WriterLock.ExpiresAt);
                        TimeSpan? timeout = default;
                        if (dateTimes.Count > 0)
                        {
                            timeout = dateTimes.Max() - DateTime.UtcNow;
                            if (timeout < TimeSpan.Zero)
                                timeout = TimeSpan.Zero;
                        }
                        if (timeout != TimeSpan.Zero)
                            await sub.WaitForChangeAsync(timeout, cancellationToken);
                    }
                } while (!await TryAcquireWriterLock(lockId, lifetime, cancellationToken));
            }
            catch
            {
                await _locks.UpdateAsync(
                    _id,
                    u => u.RemoveAll(rwl => rwl.WriterQueue, l => l.Id == lockId),
                    cancellationToken: cancellationToken
                );
                throw;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<bool> TryAcquireWriterLock(string lockId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        Expression<Func<RWLock, bool>> filter = rwl =>
            rwl.Id == _id
            && (rwl.WriterLock == null || rwl.WriterLock.ExpiresAt <= now)
            && !rwl.ReaderLocks.Any(l => l.ExpiresAt > now)
            && (!rwl.WriterQueue.Any() || rwl.WriterQueue[0].Id == lockId);
        void Update(IUpdateBuilder<RWLock> u)
        {
            u.Set(
                rwl => rwl.WriterLock,
                new Lock
                {
                    Id = lockId,
                    ExpiresAt = now + lifetime,
                    HostId = _hostId
                }
            );
            u.RemoveAll(rwl => rwl.WriterQueue, l => l.Id == lockId);
        }
        RWLock? rwLock = await _locks.UpdateAsync(filter, Update, cancellationToken: cancellationToken);
        return rwLock is not null;
    }

    private async Task TryReaderLock(string lockId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        if (!await TryAcquireReaderLock(lockId, lifetime, cancellationToken))
        {
            using ISubscription<RWLock> sub = await _locks.SubscribeAsync(rwl => rwl.Id == _id, cancellationToken);
            do
            {
                RWLock? rwLock = sub.Change.Entity;
                if (rwLock is not null && !rwLock.IsAvailableForReading())
                {
                    TimeSpan? timeout = default;
                    if (rwLock.WriterLock?.ExpiresAt is not null)
                    {
                        timeout = rwLock.WriterLock.ExpiresAt - DateTime.UtcNow;
                        if (timeout < TimeSpan.Zero)
                            timeout = TimeSpan.Zero;
                    }
                    if (timeout != TimeSpan.Zero)
                        await sub.WaitForChangeAsync(timeout, cancellationToken);
                }
            } while (!await TryAcquireReaderLock(lockId, lifetime, cancellationToken));
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<bool> TryAcquireReaderLock(string lockId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        Expression<Func<RWLock, bool>> filter = rwl =>
            rwl.Id == _id && (rwl.WriterLock == null || rwl.WriterLock.ExpiresAt <= now) && !rwl.WriterQueue.Any();
        void Update(IUpdateBuilder<RWLock> u)
        {
            u.Add(
                rwl => rwl.ReaderLocks,
                new Lock
                {
                    Id = lockId,
                    ExpiresAt = now + lifetime,
                    HostId = _hostId
                }
            );
        }

        RWLock? rwLock = await _locks.UpdateAsync(filter, Update, cancellationToken: cancellationToken);
        return rwLock is not null;
    }

    private class WriterLockReleaser(DistributedReaderWriterLock distributedLock, string lockId) : AsyncDisposableBase
    {
        private readonly DistributedReaderWriterLock _distributedLock = distributedLock;
        private readonly string _lockId = lockId;

        protected override async ValueTask DisposeAsyncCore()
        {
            Expression<Func<RWLock, bool>> filter = rwl =>
                rwl.Id == _distributedLock._id && rwl.WriterLock != null && rwl.WriterLock.Id == _lockId;
            await _distributedLock._locks.UpdateAsync(filter, u => u.Unset(rwl => rwl.WriterLock));
        }
    }

    private class ReaderLockReleaser(DistributedReaderWriterLock distributedLock, string lockId) : AsyncDisposableBase
    {
        private readonly DistributedReaderWriterLock _distributedLock = distributedLock;
        private readonly string _lockId = lockId;

        protected override async ValueTask DisposeAsyncCore()
        {
            Expression<Func<RWLock, bool>> filter = rwl =>
                rwl.Id == _distributedLock._id && rwl.ReaderLocks.Any(l => l.Id == _lockId);
            await _distributedLock._locks.UpdateAsync(
                filter,
                u => u.RemoveAll(rwl => rwl.ReaderLocks, l => l.Id == _lockId)
            );
        }
    }
}
