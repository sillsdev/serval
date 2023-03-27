namespace Serval.Webhooks.Consumers;

public class TranslationBuildStartedConsumer : IConsumer<TranslationBuildStarted>
{
    private readonly IWebhookService _webhookService;
    private readonly IUrlService _urlService;

    public TranslationBuildStartedConsumer(IWebhookService webhookService, IUrlService urlService)
    {
        _webhookService = webhookService;
        _urlService = urlService;
    }

    public async Task Consume(ConsumeContext<TranslationBuildStarted> context)
    {
        await _webhookService.SendEventAsync(
            WebhookEvent.TranslationBuildStarted,
            context.Message.Owner,
            new TranslationBuildStartedDto
            {
                Build = new ResourceLinkDto
                {
                    Id = context.Message.BuildId,
                    Url = _urlService.GetUrl(
                        "GetTranslationBuild",
                        new { id = context.Message.EngineId, buildId = context.Message.BuildId }
                    )
                },
                Engine = new ResourceLinkDto
                {
                    Id = context.Message.EngineId,
                    Url = _urlService.GetUrl("GetTranslationEngine", new { id = context.Message.EngineId })
                }
            },
            context.CancellationToken
        );
    }
}
