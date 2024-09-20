namespace Serval.Webhooks.Consumers;

public class JobFinishedConsumer(IWebhookService webhookService, IUrlService urlService) : IConsumer<BuildFinished>
{
    private readonly IWebhookService _webhookService = webhookService;
    private readonly IUrlService _urlService = urlService;

    public async Task Consume(ConsumeContext<BuildFinished> context)
    {
        switch (EngineTypeResolver.GetEngineType(context.Message.Type))
        {
            case EngineType.Translation:
                await SendBuildWebhookAsync(
                    context,
                    WebhookEvent.TranslationBuildFinished,
                    Endpoints.GetTranslationBuild,
                    Endpoints.GetTranslationEngine
                );
                break;
            case EngineType.Assessment:
                await SendBuildWebhookAsync(
                    context,
                    WebhookEvent.AssessmentBuildFinished,
                    Endpoints.GetAssessmentJob,
                    Endpoints.GetAssessmentEngine
                );
                break;
        }
    }

    private async Task SendBuildWebhookAsync(
        ConsumeContext<BuildFinished> context,
        WebhookEvent eventType,
        string jobEndpoint,
        string engineEndpoint
    )
    {
        await _webhookService.SendEventAsync(
            eventType,
            context.Message.Owner,
            new BuildFinishedDto
            {
                Build = new ResourceLinkDto
                {
                    Id = context.Message.BuildId,
                    Url = _urlService.GetUrl(
                        jobEndpoint,
                        new { id = context.Message.EngineId, buildId = context.Message.BuildId }
                    )
                },
                Engine = new ResourceLinkDto
                {
                    Id = context.Message.EngineId,
                    Url = _urlService.GetUrl(engineEndpoint, new { id = context.Message.EngineId })
                },
                BuildState = context.Message.BuildState,
                Message = context.Message.Message,
                DateFinished = context.Message.DateFinished
            },
            context.CancellationToken
        );
    }
}
