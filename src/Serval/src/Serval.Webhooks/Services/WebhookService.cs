namespace Serval.Webhooks.Services;

public class WebhookService(IRepository<Webhook> hooks, IBackgroundJobClient jobClient)
    : OwnedEntityServiceBase<Webhook>(hooks),
        IWebhookService
{
    private readonly IBackgroundJobClient _jobClient = jobClient;

    public async Task SendEventAsync(
        WebhookEvent webhookEvent,
        string owner,
        object payload,
        CancellationToken cancellationToken = default
    )
    {
        if (await Entities.ExistsAsync(h => h.Owner == owner && h.Events.Contains(webhookEvent), cancellationToken))
            _jobClient.Enqueue<WebhookJob>(j => j.RunAsync(webhookEvent, owner, payload, CancellationToken.None));
    }
}
