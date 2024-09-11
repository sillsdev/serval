using Bugsnag.AspNet.Core;
using Microsoft.Extensions.Configuration;
using SIL.ServiceToolkit.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddBugSnag(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var apiKey = configuration?.GetSection(BugsnagOptions.Key)?.GetValue<string>("ApiKey");
        if (apiKey is null)
        {
            Console.WriteLine("BugSnag ApiKey not available - not adding BugSnag.");
        }
        services.AddBugsnag(configuration =>
        {
            configuration.ApiKey = apiKey;
        });
        return services;
    }
}
