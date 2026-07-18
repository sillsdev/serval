namespace Serval.ApiKeys.Services;

public static class ApiKeyDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string PolicyScheme = "JwtOrApiKey";
    public const string HeaderName = "X-API-Key";
    public const string Issuer = "https://serval-api-keys";
    public const string KeyPrefix = "serval_";
}
