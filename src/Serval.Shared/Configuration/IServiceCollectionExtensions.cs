namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddServal(
        this IServiceCollection services,
        Action<IServalConfigurator> configure,
        IConfiguration? config = null
    )
    {
        var configurator = new ServalConfigurator(services, config);
        configure(configurator);
        return services;
    }
}
