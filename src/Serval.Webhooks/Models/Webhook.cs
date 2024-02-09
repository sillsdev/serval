namespace Serval.Webhooks.Models;

public record Webhook : IOwnedEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string Owner { get; init; }
    public required string Url { get; init; }
    public required string Secret { get; init; }
    public required List<WebhookEvent> Events { get; init; }
}
