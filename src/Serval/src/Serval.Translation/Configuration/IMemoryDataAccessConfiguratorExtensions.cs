namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddTranslationRepositories(
        this IMemoryDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<Engine>();
        configurator.AddRepository<Build>();
        configurator.AddRepository<Pretranslation>();
        return configurator;
    }
}
