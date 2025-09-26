using Serval.Health.V1;
using Serval.WordAlignment.V1;

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

        var wordAlignmentOptions = new WordAlignmentOptions();
        builder.Configuration.GetSection(WordAlignmentOptions.Key).Bind(wordAlignmentOptions);

        foreach (EngineInfo engine in wordAlignmentOptions.Engines)
        {
            builder.Services.AddGrpcClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(
                engine.Type,
                o => o.Address = new Uri(engine.Address)
            );
            builder.Services.AddGrpcClient<HealthApi.HealthApiClient>(
                $"{engine.Type}-Health",
                o => o.Address = new Uri(engine.Address)
            );
            builder.Services.AddHealthChecks().AddCheck<GrpcServiceHealthCheck>(engine.Type);
        }

        builder.Services.AddOutbox(x =>
        {
            x.AddConsumer<EngineCreateConsumer>();
            x.AddConsumer<EngineDeleteConsumer>();
            x.AddConsumer<EngineStartBuildConsumer>();
        });

        return builder;
    }
}
