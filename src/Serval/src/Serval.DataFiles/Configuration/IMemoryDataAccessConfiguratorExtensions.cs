namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddDataFilesRepositories(
        this IMemoryDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<DataFile>();
        return configurator;
    }
}
