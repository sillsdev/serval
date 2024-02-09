namespace Serval.Webhooks.Contracts;

public record WebhookConfigDto
{
    /// <summary>
    /// The payload URL.
    /// </summary>
    public required string PayloadUrl { get; init; }

    /// <summary>
    /// The shared secret.
    /// </summary>
    public required string Secret { get; init; }

    /// <summary>
    /// The webhook events.
    /// </summary>
    public required IList<WebhookEvent> Events { get; init; }
}
