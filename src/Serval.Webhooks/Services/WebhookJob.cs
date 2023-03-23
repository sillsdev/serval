namespace Serval.Webhooks.Services;

public class WebhookJob
{
    private readonly IRepository<Webhook> _hooks;
    private readonly HttpClient _httpClient;
    private readonly JsonOptions _jsonOptions;

    public WebhookJob(IRepository<Webhook> hooks, HttpClient httpClient, IOptions<JsonOptions> jsonOptions)
    {
        _hooks = hooks;
        _httpClient = httpClient;
        _jsonOptions = jsonOptions.Value;
    }

    public async Task RunAsync(
        WebhookEvent webhookEvent,
        string owner,
        object payload,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<Webhook> matchingHooks = await GetWebhooks(webhookEvent, owner, cancellationToken);
        if (matchingHooks.Count == 0)
            return;

        foreach (Webhook hook in matchingHooks)
        {
            string payloadStr = SerializePayload(webhookEvent, payload);
            var request = new HttpRequestMessage(HttpMethod.Post, hook.Url)
            {
                Content = new StringContent(payloadStr, Encoding.UTF8, "application/json")
            };
            byte[] keyBytes = Encoding.UTF8.GetBytes(hook.Secret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadStr);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hash = hmac.ComputeHash(payloadBytes);
                string signature = $"sha256={Convert.ToHexString(hash)}";
                request.Headers.Add("X-Hub-Signature-256", signature);
            }
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    private Task<IReadOnlyList<Webhook>> GetWebhooks(
        WebhookEvent webhookEvent,
        string owner,
        CancellationToken cancellationToken
    )
    {
        return _hooks.GetAllAsync(h => h.Owner == owner && h.Events.Contains(webhookEvent), cancellationToken);
    }

    private string SerializePayload(WebhookEvent webhookEvent, object payload)
    {
        return JsonSerializer.Serialize(
            new WebhookPayload
            {
                Event = webhookEvent,
                Payload = JsonSerializer.SerializeToNode(payload, _jsonOptions.JsonSerializerOptions)?.AsObject()
            },
            _jsonOptions.JsonSerializerOptions
        );
    }

    private record WebhookPayload
    {
        public WebhookEvent Event { get; init; }

        [JsonExtensionData]
        public JsonObject? Payload { get; init; }
    }
}
