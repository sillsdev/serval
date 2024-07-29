namespace Serval.Webhooks.Consumers;

public class AssessmentJobFinishedConsumer(IWebhookService webhookService, IUrlService urlService)
    : IConsumer<AssessmentJobFinished>
{
    private readonly IWebhookService _webhookService = webhookService;
    private readonly IUrlService _urlService = urlService;

    public async Task Consume(ConsumeContext<AssessmentJobFinished> context)
    {
        await _webhookService.SendEventAsync(
            WebhookEvent.AssessmentJobFinished,
            context.Message.Owner,
            new AssessmentJobFinishedDto
            {
                Job = new ResourceLinkDto
                {
                    Id = context.Message.JobId,
                    Url = _urlService.GetUrl(
                        Endpoints.GetAssessmentJob,
                        new { id = context.Message.EngineId, jobId = context.Message.JobId }
                    )
                },
                Engine = new ResourceLinkDto
                {
                    Id = context.Message.EngineId,
                    Url = _urlService.GetUrl(Endpoints.GetAssessmentEngine, new { id = context.Message.EngineId })!
                },
                JobState = context.Message.JobState,
                Message = context.Message.Message,
                DateFinished = context.Message.DateFinished
            },
            context.CancellationToken
        );
    }
}
