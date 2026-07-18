namespace Serval.ApiKeys.Services;

public class DtoMapper(IUrlService urlService)
{
    public ApiKeyDto Map(ApiKey source) =>
        new()
        {
            Id = source.Id,
            Url = urlService.GetUrl(Endpoints.GetApiKey, new { id = source.Id }),
            ClientId = source.ClientId,
            Name = source.Name,
            Scopes = [.. source.Scopes],
            ExpiresAt = source.ExpiresAt,
            CreatedAt = source.CreatedAt,
            RevokedAt = source.RevokedAt,
        };

    public ApiKeyCreatedDto Map(ApiKey source, string key) =>
        new()
        {
            Id = source.Id,
            Url = urlService.GetUrl(Endpoints.GetApiKey, new { id = source.Id }),
            ClientId = source.ClientId,
            Name = source.Name,
            Scopes = [.. source.Scopes],
            ExpiresAt = source.ExpiresAt,
            CreatedAt = source.CreatedAt,
            RevokedAt = source.RevokedAt,
            Key = key,
        };
}
