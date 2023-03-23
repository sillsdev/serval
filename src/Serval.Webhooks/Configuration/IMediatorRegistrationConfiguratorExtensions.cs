namespace Microsoft.Extensions.DependencyInjection;

public static class IMediatorRegistrationConfiguratorExtensions
{
    public static IMediatorRegistrationConfigurator AddWebhooksConsumers(
        this IMediatorRegistrationConfigurator configurator
    )
    {
        configurator.AddConsumer<TranslationBuildStartedConsumer>();
        configurator.AddConsumer<TranslationBuildFinishedConsumer>();
        return configurator;
    }
}
