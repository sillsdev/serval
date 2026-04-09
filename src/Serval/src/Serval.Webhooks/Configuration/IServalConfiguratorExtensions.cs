namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddWebhooks(this IServalConfigurator configurator)
    {
        configurator.Services.AddHttpClient<WebhookJob>();
        configurator.Services.AddScoped<IWebhookService, WebhookService>();

        configurator.AddWebhooksDataAccess();

        configurator.JobQueues.Add("webhook");

        configurator.AddHandlers(Assembly.GetExecutingAssembly());

        return configurator;
    }

    public static IServalConfigurator AddWebhooksDataAccess(this IServalConfigurator configurator)
    {
        configurator.DataAccess.AddRepository<Webhook>(
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
