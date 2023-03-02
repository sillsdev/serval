namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddTranslationRepositories(
        this IMemoryDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<TranslationEngine>();
        configurator.AddRepository<Build>();
        configurator.AddRepository<Corpus>();
        configurator.AddRepository<Pretranslation>();
        return configurator;
    }
}
