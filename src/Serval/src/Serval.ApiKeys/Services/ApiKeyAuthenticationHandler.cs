using Microsoft.Extensions.Primitives;

namespace Serval.ApiKeys.Services;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyService apiKeyService
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly IApiKeyService _apiKeyService = apiKeyService;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out StringValues values) || values.Count == 0)
            return AuthenticateResult.NoResult();

        ApiKey? apiKey = await _apiKeyService.ValidateAsync(values[0]!, Context.RequestAborted);
        if (apiKey is null)
            return AuthenticateResult.Fail("Invalid, expired, or revoked API key.");

        // an API key must never confer key management scopes, even if the stored record contains them
        IEnumerable<string> scopes = apiKey.Scopes.Intersect(Scopes.Grantable);
        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, apiKey.Owner, null, ApiKeyDefaults.Issuer),
            new Claim("scope", string.Join(' ', scopes), null, ApiKeyDefaults.Issuer),
        ];
        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, null);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
