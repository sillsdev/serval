using Serval.Health.V1;
using Serval.Translation.V1;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddTranslation(this IServalBuilder builder)
    {
        builder.AddApiOptions(builder.Configuration.GetSection(ApiOptions.Key));
        builder.AddDataFileOptions(builder.Configuration.GetSection(DataFileOptions.Key));

        builder.Services.AddScoped<IBuildService, BuildService>();
        builder.Services.AddScoped<IPretranslationService, PretranslationService>();
        builder.Services.AddScoped<IEngineService, EngineService>();

        builder.Services.AddSingleton<EngineCleanupService>();
        builder.Services.AddSingleton<BuildCleanupService>();

        var translationOptions = new TranslationOptions();
        builder.Configuration.GetSection(TranslationOptions.Key).Bind(translationOptions);

        foreach (EngineInfo engine in translationOptions.Engines)
        {
            builder.Services.AddGrpcClient<TranslationEngineApi.TranslationEngineApiClient>(
                engine.Type,
                o => o.Address = new Uri(engine.Address)
            );
            builder.Services.AddGrpcClient<HealthApi.HealthApiClient>(
                $"{engine.Type}-Health",
                o => o.Address = new Uri(engine.Address)
            );
            builder.Services.AddHealthChecks().AddCheck<GrpcServiceHealthCheck>(engine.Type);
        }

        return builder;
    }
}
