﻿namespace Serval.Webhooks.Services;

public class WebhookService(IRepository<Webhook> hooks, IBackgroundJobClient jobClient)
    : EntityServiceBase<Webhook>(hooks),
        IWebhookService
{
    private readonly IBackgroundJobClient _jobClient = jobClient;

    public async Task<IEnumerable<Webhook>> GetAllAsync(string owner, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(c => c.Owner == owner, cancellationToken);
    }

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