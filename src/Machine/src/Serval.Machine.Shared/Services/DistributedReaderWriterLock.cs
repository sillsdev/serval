namespace Serval.Machine.Shared.Services;

public class DistributedReaderWriterLock(
    string hostId,
    IRepository<RWLock> locks,
    IIdGenerator idGenerator,
    string id,
    DistributedReaderWriterLockOptions lockOptions
) : IDistributedReaderWriterLock
{
    private readonly string _hostId = hostId;
    private readonly IRepository<RWLock> _locks = locks;
    private readonly IIdGenerator _idGenerator = idGenerator;
    private readonly string _id = id;
    private readonly DistributedReaderWriterLockOptions _lockOptions = lockOptions;

    public Task ReaderLockAsync(
        Func<CancellationToken, Task> action,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default
    )
    {
        return ReaderLockAsync<object?>(
            async ct =>
            {
                await action(ct);
                return null;
            },
            lifetime,
            cancellationToken
        );
    }

    public Task WriterLockAsync(
        Func<CancellationToken, Task> action,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default
    )
    {
        return WriterLockAsync<object?>(
            async ct =>
            {
                await action(ct);
                return null;
            },
            lifetime,
            cancellationToken
        );
    }

    public async Task<T> ReaderLockAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default
    )
    {
        if (lifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                "The lifetime must be greater than or equal to zero."
            );
        }

        TimeSpan resolvedLifetime = lifetime ?? _lockOptions.DefaultLifetime;
        string lockId = _idGenerator.GenerateId();
        (bool acquired, DateTime expiresAt) = await TryAcquireReaderLock(lockId, resolvedLifetime, cancellationToken);
        if (!acquired)
        {
            using ISubscription<RWLock> sub = await _locks.SubscribeAsync(rwl => rwl.Id == _id, cancellationToken);
            do
            {
                RWLock? rwLock = sub.Change.Entity;
                if (rwLock is not null && !rwLock.IsAvailableForReading())
                {
                    TimeSpan? timeout = null;
                    if (rwLock.WriterQueue.Count == 0 && rwLock.WriterLock?.ExpiresAt is not null)
                    {
                        timeout = rwLock.WriterLock.ExpiresAt - DateTime.UtcNow;
                        if (timeout < TimeSpan.Zero)
                            timeout = TimeSpan.Zero;
                    }
                    if (timeout != TimeSpan.Zero)
                        await sub.WaitForChangeAsync(timeout, cancellationToken);
                }
                (acquired, expiresAt) = await TryAcquireReaderLock(lockId, resolvedLifetime, cancellationToken);
            } while (!acquired);
        }

        try
        {
            (bool completed, T? result) = await TaskEx.Timeout(action, expiresAt - DateTime.UtcNow, cancellationToken);
            if (!completed)
                throw new TimeoutException($"A reader lock for the distributed lock '{_id}' expired.");
            // if the task sucssfully completed, then the result will be populated
            return result!;
        }
        finally
        {
            Expression<Func<RWLock, bool>> filter = rwl => rwl.Id == _id && rwl.ReaderLocks.Any(l => l.Id == lockId);
            await _locks.UpdateAsync(
                filter,
                u => u.RemoveAll(rwl => rwl.ReaderLocks, l => l.Id == lockId),
                cancellationToken: CancellationToken.None
            );
        }
    }

    public async Task<T> WriterLockAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default
    )
    {
        if (lifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                "The lifetime must be greater than or equal to zero."
            );
        }

        TimeSpan resolvedLifetime = lifetime ?? _lockOptions.DefaultLifetime;
        string lockId = _idGenerator.GenerateId();
        (bool acquired, DateTime expiresAt) = await TryAcquireWriterLock(lockId, resolvedLifetime, cancellationToken);
        if (!acquired)
        {
            await _locks.UpdateAsync(
                _id,
                u => u.Add(rwl => rwl.WriterQueue, new Lock { Id = lockId, HostId = _hostId }),
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
                        TimeSpan? timeout = null;
                        if (rwLock.WriterQueue[0].Id == lockId)
                        {
                            var dateTimes = rwLock.ReaderLocks.Select(l => l.ExpiresAt).ToList();
                            if (rwLock.WriterLock?.ExpiresAt is not null)
                                dateTimes.Add(rwLock.WriterLock.ExpiresAt);
                            if (dateTimes.Count > 0)
                            {
                                timeout = dateTimes.Max() - DateTime.UtcNow;
                                if (timeout < TimeSpan.Zero)
                                    timeout = TimeSpan.Zero;
                            }
                        }
                        if (timeout != TimeSpan.Zero)
                            await sub.WaitForChangeAsync(timeout, cancellationToken);
                    }
                    (acquired, expiresAt) = await TryAcquireWriterLock(lockId, resolvedLifetime, cancellationToken);
                } while (!acquired);
            }
            catch
            {
                await _locks.UpdateAsync(
                    _id,
                    u => u.RemoveAll(rwl => rwl.WriterQueue, l => l.Id == lockId),
                    cancellationToken: CancellationToken.None
                );
                throw;
            }
        }

        try
        {
            (bool completed, T? result) = await TaskEx.Timeout(action, expiresAt - DateTime.UtcNow, cancellationToken);
            if (!completed)
                throw new TimeoutException($"A writer lock for the distributed lock '{_id}' expired.");
            // if the task sucssfully completed, then the result will be populated
            return result!;
        }
        finally
        {
            Expression<Func<RWLock, bool>> filter = rwl =>
                rwl.Id == _id && rwl.WriterLock != null && rwl.WriterLock.Id == lockId;
            await _locks.UpdateAsync(
                filter,
                u => u.Unset(rwl => rwl.WriterLock),
                cancellationToken: CancellationToken.None
            );
        }
    }

    private async Task<(bool, DateTime)> TryAcquireWriterLock(
        string lockId,
        TimeSpan lifetime,
        CancellationToken cancellationToken
    )
    {
        DateTime now = DateTime.UtcNow;
        DateTime expiresAt = now + lifetime;
#pragma warning disable CA1826 // Mongo LINQ3 does not support indexers
        Expression<Func<RWLock, bool>> filter = rwl =>
            rwl.Id == _id
            && (rwl.WriterLock == null || rwl.WriterLock.ExpiresAt <= now)
            && !rwl.ReaderLocks.Any(l => l.ExpiresAt > now)
            && (!rwl.WriterQueue.Any() || rwl.WriterQueue.First().Id == lockId);
#pragma warning restore CA1826
        void Update(IUpdateBuilder<RWLock> u)
        {
            u.Set(
                rwl => rwl.WriterLock,
                new Lock
                {
                    Id = lockId,
                    ExpiresAt = expiresAt,
                    HostId = _hostId
                }
            );
            u.RemoveAll(rwl => rwl.WriterQueue, l => l.Id == lockId);
        }
        RWLock? rwLock = await _locks.UpdateAsync(filter, Update, cancellationToken: cancellationToken);
        return (rwLock is not null, expiresAt);
    }

    private async Task<(bool, DateTime)> TryAcquireReaderLock(
        string lockId,
        TimeSpan lifetime,
        CancellationToken cancellationToken
    )
    {
        DateTime now = DateTime.UtcNow;
        DateTime expiresAt = now + lifetime;
        Expression<Func<RWLock, bool>> filter = rwl =>
            rwl.Id == _id && (rwl.WriterLock == null || rwl.WriterLock.ExpiresAt <= now) && !rwl.WriterQueue.Any();
        void Update(IUpdateBuilder<RWLock> u)
        {
            u.Add(
                rwl => rwl.ReaderLocks,
                new Lock
                {
                    Id = lockId,
                    ExpiresAt = expiresAt,
                    HostId = _hostId
                }
            );
        }

        RWLock? rwLock = await _locks.UpdateAsync(filter, Update, cancellationToken: cancellationToken);
        return (rwLock is not null, expiresAt);
    }
}
