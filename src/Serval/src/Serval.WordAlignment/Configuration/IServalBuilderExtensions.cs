namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWordAlignment(this IServalBuilder builder)
    {
        builder.AddApiOptions(builder.Configuration.GetSection(ApiOptions.Key));
        builder.AddDataFileOptions(builder.Configuration.GetSection(DataFileOptions.Key));

        builder.Services.AddScoped<IBuildService, BuildService>();
        builder.Services.AddScoped<IWordAlignmentService, WordAlignmentService>();
        builder.Services.AddScoped<IEngineService, EngineService>();
        builder.Services.AddScoped<IEngineServiceFactory, EngineServiceFactory>();

        builder.Services.Configure<WordAlignmentOptions>(builder.Configuration.GetSection(WordAlignmentOptions.Key));

        return builder;
    }
}
