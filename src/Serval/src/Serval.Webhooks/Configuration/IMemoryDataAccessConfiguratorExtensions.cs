namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddWebhooksRepositories(this IMemoryDataAccessConfigurator configurator)
    {
        configurator.AddRepository<Webhook>();
        return configurator;
    }
}
