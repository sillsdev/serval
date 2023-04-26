using Grpc.Health.V1;
using Serval.Translation.V1;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddTranslation(
        this IServalBuilder builder,
        Action<TranslationOptions>? configure = null
    )
    {
        if (builder.Configuration is null)
        {
            builder.AddApiOptions(o => { });
            builder.AddDataFileOptions(o => { });
        }
        else
        {
            builder.AddApiOptions(builder.Configuration.GetSection(ApiOptions.Key));
            builder.AddDataFileOptions(builder.Configuration.GetSection(DataFileOptions.Key));
        }

        builder.Services.AddScoped<IBuildService, BuildService>();
        builder.Services.AddScoped<IPretranslationService, PretranslationService>();
        builder.Services.AddScoped<IEngineService, EngineService>();

        var translationOptions = new TranslationOptions();
        if (builder.Configuration is not null)
            builder.Configuration.GetSection(TranslationOptions.Key).Bind(translationOptions);
        if (configure is not null)
            configure(translationOptions);

        foreach (EngineInfo engine in translationOptions.Engines)
        {
            builder.Services.AddGrpcClient<TranslationEngineApi.TranslationEngineApiClient>(
                engine.Type,
                o => o.Address = new Uri(engine.Address)
            );
            builder.Services.AddGrpcClient<Health.HealthClient>(
                engine.Type + "_Health",
                o => o.Address = new Uri(engine.Address)
            );
            builder.Services.AddHealthChecks().AddCheck<GrpcServiceHealthCheck>(engine.Type);
        }

        return builder;
    }
}
