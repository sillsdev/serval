using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddWebhooksRepositories(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<Webhook>(
            "hooks",
            init: c =>
            {
                c.Indexes.CreateOrUpdate(
                    new CreateIndexModel<Webhook>(Builders<Webhook>.IndexKeys.Ascending(h => h.Owner))
                );
                c.Indexes.CreateOrUpdate(
                    new CreateIndexModel<Webhook>(Builders<Webhook>.IndexKeys.Ascending(h => h.Events))
                );
            }
        );
        return configurator;
    }
}
