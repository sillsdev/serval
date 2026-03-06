using MongoDB.Driver;
using Serval.Webhooks.Models;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddWebhooksRepositories(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<Webhook>(
            "webhooks.hooks",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Webhook>(Builders<Webhook>.IndexKeys.Ascending(h => h.Owner))
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Webhook>(Builders<Webhook>.IndexKeys.Ascending(h => h.Events))
                    ),
            ]
        );
        return configurator;
    }
}
