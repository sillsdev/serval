using Serval.Assessment.V1;
using Serval.Health.V1;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddAssessment(this IServalBuilder builder, Action<AssessmentOptions>? configure = null)
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

        builder.Services.AddScoped<IJobService, JobService>();
        builder.Services.AddScoped<IResultService, ResultService>();
        builder.Services.AddScoped<IEngineService, EngineService>();
        builder.Services.AddScoped<ICorpusService, CorpusService>();

        var assessmentOptions = new AssessmentOptions();
        builder.Configuration?.GetSection(AssessmentOptions.Key).Bind(assessmentOptions);
        if (configure is not null)
            configure(assessmentOptions);

        foreach (EngineInfo engine in assessmentOptions.Engines)
        {
            builder.Services.AddGrpcClient<AssessmentEngineApi.AssessmentEngineApiClient>(
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
