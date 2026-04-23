namespace Serval.Webhooks.Services;

public interface IWebhookService
{
    Task SendEventAsync(
        WebhookEvent webhookEvent,
        string owner,
        object payload,
        CancellationToken cancellationToken = default
    );
}
