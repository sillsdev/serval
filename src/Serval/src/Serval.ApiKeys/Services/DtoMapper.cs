namespace Serval.ApiKeys.Services;

public class DtoMapper(IUrlService urlService)
{
    public ApiKeyDto Map(ApiKey source) =>
        new()
        {
            Id = source.Id,
            Url = urlService.GetUrl(Endpoints.GetApiKey, new { id = source.Id }),
            Owner = source.Owner,
            Name = source.Name,
            Scopes = [.. source.Scopes],
            ExpiresAt = source.ExpiresAt,
            CreatedAt = source.CreatedAt,
        };

    public ApiKeyCreatedDto Map(ApiKey source, string key) =>
        new()
        {
            Id = source.Id,
            Url = urlService.GetUrl(Endpoints.GetApiKey, new { id = source.Id }),
            Owner = source.Owner,
            Name = source.Name,
            Scopes = [.. source.Scopes],
            ExpiresAt = source.ExpiresAt,
            CreatedAt = source.CreatedAt,
            Key = key,
        };
}
