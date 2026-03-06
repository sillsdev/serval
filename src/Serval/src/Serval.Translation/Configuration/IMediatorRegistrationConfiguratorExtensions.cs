namespace Serval.Translation.Configuration;

public static class IMediatorRegistrationConfiguratorExtensions
{
    public static IMediatorRegistrationConfigurator AddTranslationConsumers(
        this IMediatorRegistrationConfigurator configurator
    )
    {
        configurator.AddConsumer<DataFileDeletedConsumer>();
        configurator.AddConsumer<DataFileUpdatedConsumer>();
        configurator.AddConsumer<CorpusUpdatedConsumer>();
        return configurator;
    }
}
