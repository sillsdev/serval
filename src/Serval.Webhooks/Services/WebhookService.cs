namespace Serval.Webhooks.Services;

public class WebhookService : EntityServiceBase<Webhook>, IWebhookService
{
    private readonly HttpClient _httpClient;
    private readonly JsonOptions _jsonOptions;

    public WebhookService(IRepository<Webhook> hooks, IOptions<JsonOptions> jsonOptions, HttpClient httpClient)
        : base(hooks)
    {
        _jsonOptions = jsonOptions.Value;
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<Webhook>> GetAllAsync(string owner, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(c => c.Owner == owner, cancellationToken);
    }

    public async Task SendEventAsync<T>(
        WebhookEvent webhookEvent,
        string owner,
        T payload,
        CancellationToken cancellationToken = default
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
            try
            {
                await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException) { }
        }
    }

    private Task<IReadOnlyList<Webhook>> GetWebhooks(
        WebhookEvent webhookEvent,
        string owner,
        CancellationToken cancellationToken
    )
    {
        return Entities.GetAllAsync(h => h.Owner == owner && h.Events.Contains(webhookEvent), cancellationToken);
    }

    private string SerializePayload<T>(WebhookEvent webhookEvent, T payload)
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
