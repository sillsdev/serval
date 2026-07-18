namespace Serval.ApiKeys.Services;

public class ApiKeyService(IRepository<ApiKey> apiKeys, ILogger<ApiKeyService> logger) : IApiKeyService
{
    private const int SecretSizeBytes = 32;

    private readonly IRepository<ApiKey> _apiKeys = apiKeys;
    private readonly ILogger<ApiKeyService> _logger = logger;

    public async Task<(ApiKey ApiKey, string Key)> CreateAsync(
        string clientId,
        string name,
        IEnumerable<string> scopes,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default
    )
    {
        string id = ObjectId.GenerateNewId().ToString();
        string secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretSizeBytes));
        string key = $"{ApiKeyDefaults.KeyPrefix}{id}_{secret}";
        ApiKey apiKey = new()
        {
            Id = id,
            ClientId = clientId,
            Name = name,
            HashedKey = HashKey(key),
            Scopes = [.. scopes],
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
        };
        await _apiKeys.InsertAsync(apiKey, cancellationToken);
        return (apiKey, key);
    }

    public async Task<ApiKey?> ValidateAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!key.StartsWith(ApiKeyDefaults.KeyPrefix, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected a malformed API key.");
            return null;
        }
        string[] parts = key[ApiKeyDefaults.KeyPrefix.Length..].Split('_', 2);
        if (parts.Length != 2 || !ObjectId.TryParse(parts[0], out _))
        {
            _logger.LogWarning("Rejected a malformed API key.");
            return null;
        }
        ApiKey? apiKey = await _apiKeys.GetAsync(k => k.Id == parts[0], cancellationToken);
        if (apiKey is null)
        {
            _logger.LogWarning("Rejected the API key '{Id}': the key does not exist.", parts[0]);
            return null;
        }
        byte[] hashedKey;
        try
        {
            hashedKey = Convert.FromHexString(apiKey.HashedKey);
        }
        catch (FormatException)
        {
            _logger.LogError("Rejected the API key '{Id}': the stored key hash is malformed.", apiKey.Id);
            return null;
        }
        if (!CryptographicOperations.FixedTimeEquals(hashedKey, SHA256.HashData(Encoding.UTF8.GetBytes(key))))
        {
            _logger.LogWarning("Rejected the API key '{Id}': the secret is incorrect.", apiKey.Id);
            return null;
        }
        if (apiKey.RevokedAt is not null)
        {
            _logger.LogWarning(
                "Rejected the API key '{Id}': the key was revoked at {RevokedAt:u}.",
                apiKey.Id,
                apiKey.RevokedAt
            );
            return null;
        }
        if (apiKey.ExpiresAt is not null && apiKey.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning(
                "Rejected the API key '{Id}': the key expired at {ExpiresAt:u}.",
                apiKey.Id,
                apiKey.ExpiresAt
            );
            return null;
        }
        return apiKey;
    }

    internal static string HashKey(string key)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
