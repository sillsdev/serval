namespace Serval.Webhooks.Consumers;

public class BuildStartedConsumer : IConsumer<BuildStarted>
{
    private readonly IWebhookService _webhookService;
    private readonly LinkGenerator _linkGenerator;

    public BuildStartedConsumer(IWebhookService webhookService, LinkGenerator linkGenerator)
    {
        _webhookService = webhookService;
        _linkGenerator = linkGenerator;
    }

    public async Task Consume(ConsumeContext<BuildStarted> context)
    {
        await _webhookService.SendEventAsync(
            WebhookEvent.BuildStarted,
            context.Message.Owner,
            new BuildStartedDto
            {
                Build = new ResourceLinkDto
                {
                    Id = context.Message.BuildId,
                    Url = _linkGenerator.GetPathByAction(
                        controller: "TranslationEngines",
                        action: "GetBuild",
                        values: new { id = context.Message.EngineId, buildId = context.Message.BuildId }
                    )!
                },
                Engine = new ResourceLinkDto
                {
                    Id = context.Message.EngineId,
                    Url = _linkGenerator.GetPathByAction(
                        controller: "TranslationEngines",
                        action: "Get",
                        values: new { id = context.Message.EngineId }
                    )!
                }
            },
            context.CancellationToken
        );
    }
}
