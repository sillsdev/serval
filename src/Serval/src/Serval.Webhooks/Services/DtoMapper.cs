namespace Serval.Webhooks.Services;

public class DtoMapper(IUrlService urlService)
{
    public WebhookDto Map(Webhook source) =>
        new()
        {
            Id = source.Id,
            Url = urlService.GetUrl(Endpoints.GetWebhook, new { id = source.Id }),
            PayloadUrl = source.Url,
            Events = [.. source.Events],
        };
}
