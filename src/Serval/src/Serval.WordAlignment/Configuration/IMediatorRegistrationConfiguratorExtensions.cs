namespace Microsoft.Extensions.DependencyInjection;

public static class IMediatorRegistrationConfiguratorExtensions
{
    public static IMediatorRegistrationConfigurator AddWordAlignmentConsumers(
        this IMediatorRegistrationConfigurator configurator
    )
    {
        configurator.AddConsumer<DataFileDeletedConsumer>();
        configurator.AddConsumer<DataFileUpdatedConsumer>();
        configurator.AddConsumer<CorpusUpdatedConsumer>();
        return configurator;
    }
}
