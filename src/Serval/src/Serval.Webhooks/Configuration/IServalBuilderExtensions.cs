namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWebhooks(this IServalBuilder builder)
    {
        builder.Services.AddHttpClient<WebhookJob>();
        builder.Services.AddScoped<IWebhookService, WebhookService>();

        builder.AddWebhooksDataAccess();

        builder.AddHandlers(Assembly.GetExecutingAssembly());

        return builder;
    }

    public static IServalBuilder AddWebhooksDataAccess(this IServalBuilder builder)
    {
        builder.DataAccess.AddRepository<Webhook>(
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

        return builder;
    }
}
