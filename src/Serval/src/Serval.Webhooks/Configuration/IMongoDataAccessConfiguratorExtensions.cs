using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddWebhooksRepositories(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<Webhook>(
            "webhooks.hooks",
            mapSetup: ms => ms.MapIdMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId)),
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Webhook>(Builders<Webhook>.IndexKeys.Ascending(h => h.Owner))
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Webhook>(Builders<Webhook>.IndexKeys.Ascending(h => h.Events))
                );
            }
        );
        return configurator;
    }
}
