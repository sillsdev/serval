using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWebhooks(this IServalBuilder builder)
    {
        builder.Services.AddHttpClient<WebhookJob>();
        builder.Services.AddScoped<IWebhookService, WebhookService>();

        builder.DataAccess.AddRepository<Webhook>(
            "webhooks.hooks",
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

        return builder;
    }
}
