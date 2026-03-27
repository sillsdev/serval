namespace Serval.Webhooks.Handlers;

public class WordAlignmentBuildStartedHandler(IWebhookService webhookService, IUrlService urlService)
    : IEventHandler<WordAlignmentBuildStarted>
{
    public Task HandleAsync(WordAlignmentBuildStarted message, CancellationToken cancellationToken)
    {
        return webhookService.SendEventAsync(
            WebhookEvent.WordAlignmentBuildStarted,
            message.Owner,
            new WordAlignmentBuildStartedDto
            {
                Build = new ResourceLinkDto
                {
                    Id = message.BuildId,
                    Url = urlService.GetUrl(
                        Endpoints.GetWordAlignmentBuild,
                        new { id = message.EngineId, buildId = message.BuildId }
                    ),
                },
                Engine = new ResourceLinkDto
                {
                    Id = message.EngineId,
                    Url = urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = message.EngineId }),
                },
            },
            cancellationToken
        );
    }
}
