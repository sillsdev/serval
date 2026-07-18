namespace Serval.ApiKeys.Services;

public interface IApiKeyService
{
    Task<(ApiKey ApiKey, string Key)> CreateAsync(
        string clientId,
        string name,
        IEnumerable<string> scopes,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default
    );
    Task<ApiKey?> ValidateAsync(string key, CancellationToken cancellationToken = default);
}
