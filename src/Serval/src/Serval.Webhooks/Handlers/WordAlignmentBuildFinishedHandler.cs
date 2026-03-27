namespace Serval.Webhooks.Handlers;

public class WordAlignmentBuildFinishedHandler(IWebhookService webhookService, IUrlService urlService)
    : IEventHandler<WordAlignmentBuildFinished>
{
    public Task HandleAsync(WordAlignmentBuildFinished message, CancellationToken cancellationToken)
    {
        return webhookService.SendEventAsync(
            WebhookEvent.WordAlignmentBuildFinished,
            message.Owner,
            new WordAlignmentBuildFinishedDto
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
                    Url = urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = message.EngineId })!,
                },
                BuildState = message.BuildState,
                Message = message.Message,
                DateFinished = message.DateFinished,
            },
            cancellationToken
        );
    }
}
