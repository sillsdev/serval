namespace SIL.ServiceToolkit.Services;

public class OutboxHealthCheck(IMemoryCache cache, IOptions<OutboxOptions> options, IRepository<OutboxMessage> messages)
    : IHealthCheck
{
    private const string FailureCountKey = "OutboxHealthCheck.ConsecutiveFailures";
    private readonly AsyncLock _lock = new AsyncLock();

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        int count = (await messages.GetAllAsync(cancellationToken)).Count;
        if (count > options.Value.HealthyMessageLimit)
        {
            using (await _lock.LockAsync(cancellationToken))
            {
                int numConsecutiveFailures = cache.Get<int>(FailureCountKey);
                cache.Set(FailureCountKey, ++numConsecutiveFailures);
                return numConsecutiveFailures > 3
                    ? HealthCheckResult.Unhealthy("Outbox messages are accumulating.")
                    : HealthCheckResult.Degraded("Outbox messages are accumulating.");
            }
        }
        using (await _lock.LockAsync(cancellationToken))
            cache.Set(FailureCountKey, 0);
        return HealthCheckResult.Healthy("Outbox messages are being processed successfully");
    }
}
