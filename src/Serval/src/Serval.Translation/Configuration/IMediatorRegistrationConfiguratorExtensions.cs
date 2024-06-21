namespace Microsoft.Extensions.DependencyInjection;

public static class IMediatorRegistrationConfiguratorExtensions
{
    public static IMediatorRegistrationConfigurator AddTranslationConsumers(
        this IMediatorRegistrationConfigurator configurator
    )
    {
        configurator.AddConsumer<DataFileDeletedConsumer>();
        return configurator;
    }
}
