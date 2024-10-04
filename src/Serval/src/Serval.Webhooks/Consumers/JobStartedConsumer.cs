namespace Serval.Webhooks.Consumers;

public class JobStartedConsumer(IWebhookService webhookService, IUrlService urlService) : IConsumer<JobStarted>
{
    private readonly IWebhookService _webhookService = webhookService;
    private readonly IUrlService _urlService = urlService;

    public async Task Consume(ConsumeContext<JobStarted> context)
    {
        switch (EngineTypeResolver.GetEngineType(context.Message.Type))
        {
            case EngineType.Translation:
                await SendBuildWebhookAsync(
                    context,
                    WebhookEvent.TranslationBuildStarted,
                    Endpoints.GetTranslationBuild,
                    Endpoints.GetTranslationEngine
                );
                break;
            case EngineType.Assessment:
                await SendJobWebhookAsync(
                    context,
                    WebhookEvent.AssessmentJobStarted,
                    Endpoints.GetAssessmentJob,
                    Endpoints.GetAssessmentEngine
                );
                break;
        }
    }

    private async Task SendBuildWebhookAsync(
        ConsumeContext<JobStarted> context,
        WebhookEvent eventType,
        string jobEndpoint,
        string engineEndpoint
    )
    {
        await _webhookService.SendEventAsync(
            eventType,
            context.Message.Owner,
            new BuildStartedDto
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
                }
            },
            context.CancellationToken
        );
    }

    private async Task SendJobWebhookAsync(
        ConsumeContext<JobStarted> context,
        WebhookEvent eventType,
        string jobEndpoint,
        string engineEndpoint
    )
    {
        await _webhookService.SendEventAsync(
            eventType,
            context.Message.Owner,
            new JobStartedDto
            {
                Job = new ResourceLinkDto
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
                }
            },
            context.CancellationToken
        );
    }
}
