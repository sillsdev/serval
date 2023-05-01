using Newtonsoft.Json;

namespace Serval.Webhooks.Contracts;

public class WebhookConfigDto
{
    /// <summary>
    /// The payload URL.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string PayloadUrl { get; set; } = default!;

    /// <summary>
    /// The shared secret.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string Secret { get; set; } = default!;

    /// <summary>
    /// The webhook events.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public IList<WebhookEvent> Events { get; set; } = default!;
}
