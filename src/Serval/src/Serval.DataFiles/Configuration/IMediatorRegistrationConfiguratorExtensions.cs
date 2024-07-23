namespace Microsoft.Extensions.DependencyInjection;

public static class IMediatorRegistrationConfiguratorExtensions
{
    public static IMediatorRegistrationConfigurator AddDataFilesConsumers(
        this IMediatorRegistrationConfigurator configurator
    )
    {
        configurator.AddConsumer<GetDataFileConsumer>();
        configurator.AddConsumer<DeleteDataFileConsumer>();
        return configurator;
    }
}
