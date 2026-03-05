namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServalBuilder AddServal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFileSystem();
        return new ServalBuilder(services, configuration);
    }
}
