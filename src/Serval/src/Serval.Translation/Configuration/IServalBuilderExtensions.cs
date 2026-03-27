namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddTranslation(this IServalBuilder builder)
    {
        builder.AddApiOptions(builder.Configuration.GetSection(ApiOptions.Key));
        builder.AddDataFileOptions(builder.Configuration.GetSection(DataFileOptions.Key));

        builder.Services.AddParallelCorpusService();

        builder.Services.AddScoped<IBuildService, BuildService>();
        builder.Services.AddScoped<ICorpusMappingService, CorpusMappingService>();
        builder.Services.AddScoped<IPretranslationService, PretranslationService>();
        builder.Services.AddScoped<IEngineService, EngineService>();

        builder.Services.Configure<TranslationOptions>(builder.Configuration.GetSection(TranslationOptions.Key));

        return builder;
    }
}
