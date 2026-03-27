namespace Serval.Webhooks.Handlers;

public class TranslationBuildFinishedHandler(IWebhookService webhookService, IUrlService urlService)
    : IEventHandler<TranslationBuildFinished>
{
    public Task HandleAsync(TranslationBuildFinished message, CancellationToken cancellationToken)
    {
        return webhookService.SendEventAsync(
            WebhookEvent.TranslationBuildFinished,
            message.Owner,
            new TranslationBuildFinishedDto
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
                    Url = urlService.GetUrl(Endpoints.GetTranslationEngine, new { id = message.EngineId })!,
                },
                BuildState = message.BuildState,
                Message = message.Message,
                DateFinished = message.DateFinished,
            },
            cancellationToken
        );
    }
}
