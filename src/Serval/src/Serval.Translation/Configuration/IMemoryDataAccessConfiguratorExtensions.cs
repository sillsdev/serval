namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddTranslationRepositories(
        this IMemoryDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<TranslationEngine>();
        configurator.AddRepository<TranslationBuild>();
        configurator.AddRepository<Pretranslation>();
        return configurator;
    }
}
