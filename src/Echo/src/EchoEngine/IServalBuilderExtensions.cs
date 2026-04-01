namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddEchoEngines(this IServalBuilder builder)
    {
        builder.Services.AddHostedService<BackgroundTaskService>();
        builder.Services.AddSingleton<BackgroundTaskQueue>();
        builder.AddTranslationEngine<TranslationEngineService>("echo");
        builder.AddWordAlignmentEngine<WordAlignmentEngineService>("echo");
        return builder;
    }
}
