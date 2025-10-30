namespace SIL.ServiceToolkit.Services;

public class OutboxHealthCheck(IOptions<OutboxOptions> options, IRepository<OutboxMessage> messages) : IHealthCheck
{
    private readonly IOptions<OutboxOptions> _options = options;
    private readonly IRepository<OutboxMessage> _messages = messages;
    private int _numConsecutiveFailures = 0;
    private readonly AsyncLock _lock = new AsyncLock();

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        int count = (await _messages.GetAllAsync(cancellationToken)).Count;
        if (count > _options.Value.HealthyMessageLimit)
        {
            using (await _lock.LockAsync(cancellationToken))
            {
                _numConsecutiveFailures++;
                return _numConsecutiveFailures > 3
                    ? HealthCheckResult.Unhealthy("Outbox messages are accumulating.")
                    : HealthCheckResult.Degraded("Outbox messages are accumulating.");
            }
        }
        using (await _lock.LockAsync(cancellationToken))
            _numConsecutiveFailures = 0;
        return HealthCheckResult.Healthy("Outbox messages are being processed successfully");
    }
}
