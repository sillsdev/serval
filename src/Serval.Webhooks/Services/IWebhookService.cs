namespace Serval.Webhooks.Services;

public interface IWebhookService
{
    Task<IEnumerable<Webhook>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<Webhook?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task CreateAsync(Webhook hook, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task SendEventAsync<T>(
        WebhookEvent webhookEvent,
        string owner,
        T resource,
        CancellationToken cancellationToken = default
    );
}
