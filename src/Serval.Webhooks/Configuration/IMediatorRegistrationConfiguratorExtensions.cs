namespace Microsoft.Extensions.DependencyInjection;

public static class IMediatorRegistrationConfiguratorExtensions
{
    public static IMediatorRegistrationConfigurator AddWebhooksConsumers(
        this IMediatorRegistrationConfigurator configurator
    )
    {
        configurator.AddConsumer<BuildStartedConsumer>();
        configurator.AddConsumer<BuildFinishedConsumer>();
        return configurator;
    }
}
