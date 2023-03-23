namespace Serval.Webhooks.Consumers;

public class BuildFinishedConsumer : IConsumer<BuildFinished>
{
    private readonly IWebhookService _webhookService;
    private readonly LinkGenerator _linkGenerator;

    public BuildFinishedConsumer(IWebhookService webhookService, LinkGenerator linkGenerator)
    {
        _webhookService = webhookService;
        _linkGenerator = linkGenerator;
    }

    public async Task Consume(ConsumeContext<BuildFinished> context)
    {
        await _webhookService.SendEventAsync(
            WebhookEvent.BuildFinished,
            context.Message.Owner,
            new BuildFinishedDto
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
                },
                BuildState = context.Message.BuildState,
                DateFinished = context.Message.DateFinished
            },
            context.CancellationToken
        );
    }
}
