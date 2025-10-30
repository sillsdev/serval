namespace SIL.ServiceToolkit.Services;

public class OutboxHealthCheck(IOptions<OutboxOptions> options, IRepository<OutboxMessage> messages) : IHealthCheck
{
    private readonly IOptions<OutboxOptions> _options = options;
    private readonly IRepository<OutboxMessage> _messages = messages;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        int count = (await _messages.GetAllAsync(cancellationToken)).Count;
        if (count > _options.Value.HealthyMessageLimit)
        {
            return HealthCheckResult.Unhealthy("Outbox messages are accumulating.");
        }
        return HealthCheckResult.Healthy("Outbox messages are being processed successfully");
    }
}
