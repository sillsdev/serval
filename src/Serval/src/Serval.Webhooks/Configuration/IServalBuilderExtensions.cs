namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWebhooks(this IServalBuilder builder)
    {
        builder.Services.AddHttpClient<WebhookJob>();
        builder.Services.AddScoped<IWebhookService, WebhookService>();

        builder.AddMongoDataAccess();

        builder.AddHandlers(Assembly.GetExecutingAssembly());

        return builder;
    }

    private static IServalBuilder AddMongoDataAccess(this IServalBuilder builder)
    {
        string databaseName = builder.GetDatabaseName();
        builder.DataAccess.AddRepository<Webhook>(
            databaseName,
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
