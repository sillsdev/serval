namespace Serval.Webhooks.Consumers;

public class JobFinishedConsumer(IWebhookService webhookService, IUrlService urlService) : IConsumer<JobFinished>
{
    private readonly IWebhookService _webhookService = webhookService;
    private readonly IUrlService _urlService = urlService;

    public async Task Consume(ConsumeContext<JobFinished> context)
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
                await SendJobWebhookAsync(
                    context,
                    WebhookEvent.AssessmentJobFinished,
                    Endpoints.GetAssessmentJob,
                    Endpoints.GetAssessmentEngine
                );
                break;
        }
    }

    private async Task SendBuildWebhookAsync(
        ConsumeContext<JobFinished> context,
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
                    Id = context.Message.JobId,
                    Url = _urlService.GetUrl(
                        jobEndpoint,
                        new { id = context.Message.EngineId, buildId = context.Message.JobId }
                    )
                },
                Engine = new ResourceLinkDto
                {
                    Id = context.Message.EngineId,
                    Url = _urlService.GetUrl(engineEndpoint, new { id = context.Message.EngineId })
                },
                BuildState = context.Message.JobState,
                Message = context.Message.Message,
                DateFinished = context.Message.DateFinished
            },
            context.CancellationToken
        );
    }

    private async Task SendJobWebhookAsync(
        ConsumeContext<JobFinished> context,
        WebhookEvent eventType,
        string jobEndpoint,
        string engineEndpoint
    )
    {
        await _webhookService.SendEventAsync(
            eventType,
            context.Message.Owner,
            new JobFinishedDto
            {
                Job = new ResourceLinkDto
                {
                    Id = context.Message.JobId,
                    Url = _urlService.GetUrl(
                        jobEndpoint,
                        new { id = context.Message.EngineId, buildId = context.Message.JobId }
                    )
                },
                Engine = new ResourceLinkDto
                {
                    Id = context.Message.EngineId,
                    Url = _urlService.GetUrl(engineEndpoint, new { id = context.Message.EngineId })
                },
                JobState = context.Message.JobState,
                Message = context.Message.Message,
                DateFinished = context.Message.DateFinished
            },
            context.CancellationToken
        );
    }
}
