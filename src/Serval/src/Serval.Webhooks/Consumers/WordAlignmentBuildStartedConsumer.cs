namespace Serval.Webhooks.Consumers;

public class WordAlignmentBuildStartedConsumer(IWebhookService webhookService, IUrlService urlService)
    : IConsumer<WordAlignmentBuildStarted>
{
    private readonly IWebhookService _webhookService = webhookService;
    private readonly IUrlService _urlService = urlService;

    public async Task Consume(ConsumeContext<WordAlignmentBuildStarted> context)
    {
        await _webhookService.SendEventAsync(
            WebhookEvent.WordAlignmentBuildStarted,
            context.Message.Owner,
            new WordAlignmentBuildStartedDto
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
                    Url = _urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = context.Message.EngineId }),
                },
            },
            context.CancellationToken
        );
    }
}
