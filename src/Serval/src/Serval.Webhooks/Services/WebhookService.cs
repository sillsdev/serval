namespace Serval.Webhooks.Services;

public class WebhookService(IRepository<Webhook> webhooks, IBackgroundJobClient jobClient) : IWebhookService
{
    public async Task SendEventAsync(
        WebhookEvent webhookEvent,
        string owner,
        object payload,
        CancellationToken cancellationToken = default
    )
    {
        if (await webhooks.ExistsAsync(h => h.Owner == owner && h.Events.Contains(webhookEvent), cancellationToken))
        {
            jobClient.Enqueue<WebhookJob>(
                "webhook",
                j => j.RunAsync(webhookEvent, owner, payload, CancellationToken.None)
            );
        }
    }
}
