namespace Serval.Shared.Services;

public interface IUrlService
{
    string GetUrl(string endpointName, object ids, string? version = null);
}
