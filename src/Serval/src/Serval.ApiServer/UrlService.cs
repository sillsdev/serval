using System.ComponentModel;

namespace Serval.ApiServer;

public class UrlService(LinkGenerator linkGenerator) : IUrlService
{
    private readonly LinkGenerator _linkGenerator = linkGenerator;

    public string GetUrl(string endpointName, object ids, string? version = null)
    {
        var values = new Dictionary<string, object?>();
        foreach (PropertyDescriptor propertyDescriptor in TypeDescriptor.GetProperties(ids))
        {
            object? obj = propertyDescriptor.GetValue(ids);
            values.Add(propertyDescriptor.Name, obj);
        }
        values["version"] = version ?? "1";
        string? url = _linkGenerator.GetPathByName(endpointName, values);
        if (url is null)
            throw new ArgumentException("The specified endpoint does not exist.", nameof(endpointName));
        return url;
    }
}
