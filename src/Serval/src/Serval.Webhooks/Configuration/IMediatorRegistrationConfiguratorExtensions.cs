namespace Microsoft.Extensions.DependencyInjection;

public static class IMediatorRegistrationConfiguratorExtensions
{
    public static IMediatorRegistrationConfigurator AddWebhooksConsumers(
        this IMediatorRegistrationConfigurator configurator
    )
    {
        configurator.AddConsumer<JobStartedConsumer>();
        configurator.AddConsumer<JobFinishedConsumer>();
        return configurator;
    }
}
