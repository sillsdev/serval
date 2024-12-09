namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddWordAlignmentRepositories(
        this IMemoryDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<Engine>();
        configurator.AddRepository<Build>();
        configurator.AddRepository<WordAlignment>();
        return configurator;
    }
}
