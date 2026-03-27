namespace Serval.Webhooks.Handlers;

public class TranslationBuildStartedHandler(IWebhookService webhookService, IUrlService urlService)
    : IEventHandler<TranslationBuildStarted>
{
    public Task HandleAsync(TranslationBuildStarted message, CancellationToken cancellationToken)
    {
        return webhookService.SendEventAsync(
            WebhookEvent.TranslationBuildStarted,
            message.Owner,
            new TranslationBuildStartedDto
            {
                Build = new ResourceLinkDto
                {
                    Id = message.BuildId,
                    Url = urlService.GetUrl(
                        Endpoints.GetTranslationBuild,
                        new { id = message.EngineId, buildId = message.BuildId }
                    ),
                },
                Engine = new ResourceLinkDto
                {
                    Id = message.EngineId,
                    Url = urlService.GetUrl(Endpoints.GetTranslationEngine, new { id = message.EngineId }),
                },
            },
            cancellationToken
        );
    }
}
