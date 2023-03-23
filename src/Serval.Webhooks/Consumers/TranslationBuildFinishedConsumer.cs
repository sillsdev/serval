namespace Serval.Webhooks.Consumers;

public class TranslationBuildFinishedConsumer : IConsumer<TranslationBuildFinished>
{
    private readonly IWebhookService _webhookService;
    private readonly IUrlService _urlService;

    public TranslationBuildFinishedConsumer(IWebhookService webhookService, IUrlService urlService)
    {
        _webhookService = webhookService;
        _urlService = urlService;
    }

    public async Task Consume(ConsumeContext<TranslationBuildFinished> context)
    {
        await _webhookService.SendEventAsync(
            WebhookEvent.TranslationBuildFinished,
            context.Message.Owner,
            new TranslationBuildFinishedDto
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
                    Url = _urlService.GetUrl("GetTranslationEngine", new { id = context.Message.EngineId })!
                },
                BuildState = context.Message.BuildState,
                DateFinished = context.Message.DateFinished
            },
            context.CancellationToken
        );
    }
}
