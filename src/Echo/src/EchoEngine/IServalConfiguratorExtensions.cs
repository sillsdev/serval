namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddEchoEngines(this IServalConfigurator configurator)
    {
        configurator.Services.AddHostedService<BackgroundTaskService>();
        configurator.Services.AddSingleton<BackgroundTaskQueue>();
        configurator.AddTranslationEngine<TranslationEngineService>("echo");
        configurator.AddWordAlignmentEngine<WordAlignmentEngineService>("echo");
        return configurator;
    }
}
