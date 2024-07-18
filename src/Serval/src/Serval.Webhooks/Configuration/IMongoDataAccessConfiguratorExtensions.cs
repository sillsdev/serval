using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddWebhooksRepositories(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<Webhook>(
            "webhooks.hooks",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Webhook>(
                        Builders<Webhook>.IndexKeys.Ascending(h => h.Owner).Ascending(h => h.Events)
                    )
                );
            }
        );
        return configurator;
    }
}
