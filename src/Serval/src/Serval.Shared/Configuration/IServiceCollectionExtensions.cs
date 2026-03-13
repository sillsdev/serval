using Microsoft.Extensions.DependencyInjection;

namespace Serval.Shared.Configuration;

public static class IServiceCollectionExtensions
{
    public static IServalBuilder AddServal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFileSystem();
        return new ServalBuilder(services, configuration);
    }
}
