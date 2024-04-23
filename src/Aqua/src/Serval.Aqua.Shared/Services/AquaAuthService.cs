namespace Serval.Aqua.Shared.Services;

public class AquaAuthService(IHttpClientFactory httpClientFactory, IOptionsMonitor<AquaOptions> options)
    : IAquaAuthService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) }
        };

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Aqua-NoAuth");
    private readonly IOptionsMonitor<AquaOptions> _options = options;
    private readonly AsyncLock _lock = new();
    private JwtSecurityToken? _accessToken;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        using (await _lock.LockAsync(cancellationToken))
        {
            if (_accessToken is null || _accessToken.ValidTo < DateTime.UtcNow)
                _accessToken = await AuthorizeAsync(cancellationToken);
        }
        return _accessToken.RawData;
    }

    public async Task RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        using (await _lock.LockAsync(cancellationToken))
        {
            _accessToken = await AuthorizeAsync(cancellationToken);
        }
    }

    private async Task<JwtSecurityToken> AuthorizeAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, string> parameters =
            new() { ["username"] = _options.CurrentValue.Username, ["password"] = _options.CurrentValue.Password };
        FormUrlEncodedContent content = new(parameters);
        HttpResponseMessage response = await _httpClient.PostAsync("token", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        TokenDto? token = await response.Content.ReadFromJsonAsync<TokenDto>(JsonSerializerOptions, cancellationToken);
        if (token is null)
            throw new InvalidOperationException("The AQuA authentication response is invalid.");
        JwtSecurityTokenHandler handler = new();
        return handler.ReadJwtToken(token.AccessToken);
    }
}
