namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddTranslationEngine<TEngineService>(
        this IServalConfigurator configurator,
        string engineType
    )
        where TEngineService : class, ITranslationEngineService
    {
        configurator.Services.AddKeyedScoped<ITranslationEngineService, TEngineService>(engineType.ToLowerInvariant());
        return configurator;
    }
}
