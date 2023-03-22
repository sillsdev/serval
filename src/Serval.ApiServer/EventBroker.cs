namespace Serval.ApiServer;

public class EventBroker : IEventBroker
{
    private readonly IWebhookService _webhookService;
    private readonly ITranslationEngineService _translationEngineService;
    private readonly LinkGenerator _linkGenerator;

    public EventBroker(
        IWebhookService webhookService,
        ITranslationEngineService translationEngineService,
        LinkGenerator linkGenerator
    )
    {
        _webhookService = webhookService;
        _translationEngineService = translationEngineService;
        _linkGenerator = linkGenerator;
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
                            Url = _linkGenerator.GetPathByAction(
                                controller: "TranslationEngines",
                                action: "GetBuild",
                                values: new { id = buildStarted.EngineId, buildId = buildStarted.BuildId }
                            )!
                        },
                        Engine = new ResourceLinkDto
                        {
                            Id = buildStarted.EngineId,
                            Url = _linkGenerator.GetPathByAction(
                                controller: "TranslationEngines",
                                action: "Get",
                                values: new { id = buildStarted.EngineId }
                            )!
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
                            Url = _linkGenerator.GetPathByAction(
                                controller: "TranslationEngines",
                                action: "GetBuild",
                                values: new { id = buildFinished.EngineId, buildId = buildFinished.BuildId }
                            )!
                        },
                        Engine = new ResourceLinkDto
                        {
                            Id = buildFinished.EngineId,
                            Url = _linkGenerator.GetPathByAction(
                                controller: "TranslationEngines",
                                action: "Get",
                                values: new { id = buildFinished.EngineId }
                            )!
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
