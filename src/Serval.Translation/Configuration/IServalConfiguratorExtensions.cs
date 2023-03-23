using Serval.Translation.V1;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddTranslation(
        this IServalConfigurator configurator,
        Action<TranslationOptions>? configure = null
    )
    {
        if (configurator.Configuration is null)
        {
            configurator.AddApiOptions(o => { });
            configurator.AddDataFileOptions(o => { });
        }
        else
        {
            configurator.AddApiOptions(configurator.Configuration.GetSection(ApiOptions.Key));
            configurator.AddDataFileOptions(configurator.Configuration.GetSection(DataFileOptions.Key));
        }

        configurator.Services.AddAutoMapper(Assembly.GetExecutingAssembly());

        configurator.Services.AddScoped<IBuildService, BuildService>();
        configurator.Services.AddScoped<IPretranslationService, PretranslationService>();
        configurator.Services.AddScoped<IEngineService, EngineService>();

        var translationOptions = new TranslationOptions();
        if (configurator.Configuration is not null)
            configurator.Configuration.GetSection(TranslationOptions.Key).Bind(translationOptions);
        if (configure is not null)
            configure(translationOptions);

        foreach (EngineInfo engine in translationOptions.Engines)
        {
            configurator.Services.AddGrpcClient<TranslationEngineApi.TranslationEngineApiClient>(
                engine.Type,
                o => o.Address = new Uri(engine.Address)
            );
        }

        return configurator;
    }
}
