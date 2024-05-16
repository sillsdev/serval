namespace Serval.Webhooks.Services;

public interface IWebhookService
{
    Task<IEnumerable<Webhook>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<Webhook> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<Webhook> CreateAsync(Webhook hook, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task SendEventAsync(
        WebhookEvent webhookEvent,
        string owner,
        object payload,
        CancellationToken cancellationToken = default
    );
}
