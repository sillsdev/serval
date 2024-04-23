namespace Serval.Webhooks.Consumers;

public class AssessmentJobStartedConsumer(IWebhookService webhookService, IUrlService urlService)
    : IConsumer<AssessmentJobStarted>
{
    private readonly IWebhookService _webhookService = webhookService;
    private readonly IUrlService _urlService = urlService;

    public async Task Consume(ConsumeContext<AssessmentJobStarted> context)
    {
        await _webhookService.SendEventAsync(
            WebhookEvent.AssessmentJobStarted,
            context.Message.Owner,
            new AssessmentJobStartedDto
            {
                Job = new ResourceLinkDto
                {
                    Id = context.Message.JobId,
                    Url = _urlService.GetUrl(
                        "GetAssessmentJob",
                        new { id = context.Message.EngineId, jobId = context.Message.JobId }
                    )
                },
                Engine = new ResourceLinkDto
                {
                    Id = context.Message.EngineId,
                    Url = _urlService.GetUrl("GetAssessmentEngine", new { id = context.Message.EngineId })
                }
            },
            context.CancellationToken
        );
    }
}
