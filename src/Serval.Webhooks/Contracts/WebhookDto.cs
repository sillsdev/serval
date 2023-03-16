namespace Serval.Webhooks.Contracts;

public class WebhookDto
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string PayloadUrl { get; set; } = default!;
    public WebhookEvent[] Events { get; set; } = default!;
}
