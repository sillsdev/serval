namespace Serval.Webhooks.Contracts;

public record WebhookDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required string PayloadUrl { get; init; }
    public required IList<WebhookEvent> Events { get; init; }
}
