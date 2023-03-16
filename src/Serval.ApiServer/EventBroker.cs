using Serval.Shared.Contracts;
using Serval.Shared.Services;
using Serval.Translation.Services;
using Serval.Webhooks.Contracts;
using Serval.Webhooks.Services;

namespace Serval.ApiServer;

public class EventBroker : IEventBroker
{
    private readonly IWebhookService _webhookService;
    private readonly ITranslationEngineService _translationEngineService;

    public EventBroker(IWebhookService webhookService, ITranslationEngineService translationEngineService)
    {
        _webhookService = webhookService;
        _translationEngineService = translationEngineService;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
    {
        switch (@event)
        {
            case BuildStarted buildStarted:
                await _webhookService.SendEventAsync(
                    WebhookEvent.BuildStarted,
                    buildStarted.Owner,
                    new { buildStarted.BuildId, buildStarted.EngineId },
                    cancellationToken
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
                    },
                    cancellationToken
                );
                break;

            case DataFileDeleted dataFileDeleted:
                await _translationEngineService.DeleteAllCorpusFilesAsync(
                    dataFileDeleted.DataFileId,
                    cancellationToken
                );
                break;
        }
    }
}
