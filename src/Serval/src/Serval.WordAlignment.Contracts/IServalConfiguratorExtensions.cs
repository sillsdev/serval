namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddWordAlignmentEngine<TEngineService>(
        this IServalConfigurator configurator,
        string engineType
    )
        where TEngineService : class, IWordAlignmentEngineService
    {
        configurator.Services.AddKeyedScoped<IWordAlignmentEngineService, TEngineService>(
            engineType.ToLowerInvariant()
        );
        return configurator;
    }
}
