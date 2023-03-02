using Serval.Core;
using Serval.Shared.Events;
using Serval.Shared.Services;
using Serval.Webhooks.Services;

namespace Serval.ApiServer;

public class EventBroker : IEventBroker
{
    private readonly IWebhookService _webhookService;

    public EventBroker(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    public async Task PublishAsync<T>(T @event)
    {
        switch (@event)
        {
            case BuildStarted buildStarted:
                await _webhookService.SendEventAsync(
                    WebhookEvent.BuildStarted,
                    buildStarted.Owner,
                    new { buildStarted.BuildId, buildStarted.EngineId }
                );
                break;

            case BuildFinished buildFinished:
                await _webhookService.SendEventAsync(
                    WebhookEvent.BuildFinished,
                    buildFinished.Owner,
                    new
                    {
                        buildFinished.BuildId,
                        buildFinished.EngineId,
                        buildFinished.BuildState,
                        buildFinished.DateFinished
                    }
                );
                break;
        }
    }
}
