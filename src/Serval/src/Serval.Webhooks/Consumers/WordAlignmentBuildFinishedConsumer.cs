namespace Serval.Webhooks.Consumers;

public class WordAlignmentBuildFinishedConsumer(IWebhookService webhookService, IUrlService urlService)
    : IConsumer<WordAlignmentBuildFinished>
{
    private readonly IWebhookService _webhookService = webhookService;
    private readonly IUrlService _urlService = urlService;

    public async Task Consume(ConsumeContext<WordAlignmentBuildFinished> context)
    {
        await _webhookService.SendEventAsync(
            WebhookEvent.WordAlignmentBuildFinished,
            context.Message.Owner,
            new WordAlignmentBuildFinishedDto
            {
                Build = new ResourceLinkDto
                {
                    Id = context.Message.BuildId,
                    Url = _urlService.GetUrl(
                        Endpoints.GetWordAlignmentBuild,
                        new { id = context.Message.EngineId, buildId = context.Message.BuildId }
                    ),
                },
                Engine = new ResourceLinkDto
                {
                    Id = context.Message.EngineId,
                    Url = _urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = context.Message.EngineId })!,
                },
                BuildState = context.Message.BuildState,
                Message = context.Message.Message,
                DateFinished = context.Message.DateFinished,
            },
            context.CancellationToken
        );
    }
}
