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
                    new
                    {
                        Build = new ResourceLinkDto
                        {
                            Id = buildStarted.BuildId,
                            Url = $"{Urls.TranslationEngines}/builds/{buildStarted.BuildId}"
                        },
                        Engine = new ResourceLinkDto
                        {
                            Id = buildStarted.EngineId,
                            Url = $"{Urls.TranslationEngines}/{buildStarted.EngineId}"
                        }
                    },
                    cancellationToken
                );
                break;

            case BuildFinished buildFinished:
                await _webhookService.SendEventAsync(
                    WebhookEvent.BuildFinished,
                    buildFinished.Owner,
                    new
                    {
                        Build = new ResourceLinkDto
                        {
                            Id = buildFinished.BuildId,
                            Url = $"{Urls.TranslationEngines}/builds/{buildFinished.BuildId}"
                        },
                        Engine = new ResourceLinkDto
                        {
                            Id = buildFinished.EngineId,
                            Url = $"{Urls.TranslationEngines}/{buildFinished.EngineId}"
                        },
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
