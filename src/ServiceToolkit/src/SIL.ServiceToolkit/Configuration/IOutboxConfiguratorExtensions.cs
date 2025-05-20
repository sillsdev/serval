namespace Microsoft.Extensions.DependencyInjection;

public static class IOutboxConfiguratorExtensions
{
    public static IOutboxConfigurator AddConsumer<T>(this IOutboxConfigurator configurator)
        where T : class, IOutboxConsumer
    {
        configurator.Services.AddScoped<IOutboxConsumer, T>();
        return configurator;
    }

    public static IOutboxConfigurator UseMongo(this IOutboxConfigurator configurator, string connectionString)
    {
        configurator.Services.AddMongoDataAccess(
            connectionString,
            "SIL.ServiceToolkit.Models",
            o =>
            {
                o.AddRepository<OutboxMessage>(
                    "outbox_messages",
                    mapSetup: m => m.MapProperty(m => m.OutboxRef).SetSerializer(new StringSerializer())
                );
                o.AddRepository<Outbox>(
                    "outboxes",
                    mapSetup: m => m.MapIdProperty(o => o.Id).SetSerializer(new StringSerializer())
                );
            }
        );
        return configurator;
    }

    public static IOutboxConfigurator UseDeliveryService(this IOutboxConfigurator configurator)
    {
        configurator.Services.AddHostedService<OutboxDeliveryService>();
        return configurator;
    }
}
