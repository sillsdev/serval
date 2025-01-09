namespace Microsoft.Extensions.DependencyInjection;

public static class IMediatorRegistrationConfiguratorExtensions
{
    public static IMediatorRegistrationConfigurator AddDataFilesConsumers(
        this IMediatorRegistrationConfigurator configurator
    )
    {
        configurator.AddConsumer<GetCorpusConsumer>();
        configurator.AddConsumer<GetDataFileConsumer>();
        configurator.AddConsumer<DeleteDataFileConsumer>();
        configurator.AddConsumer<DataFileDeletedConsumer>();
        return configurator;
    }
}
