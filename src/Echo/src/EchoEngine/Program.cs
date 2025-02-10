using EchoTranslationEngine;
using EchoWordAlignmentEngine;
using Serval.Translation.V1;
using Serval.WordAlignment.V1;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpcClient<TranslationPlatformApi.TranslationPlatformApiClient>(
    "Translation",
    o =>
    {
        o.Address = new Uri(builder.Configuration.GetConnectionString("TranslationPlatformApi")!);
    }
);
builder.Services.AddGrpcClient<WordAlignmentPlatformApi.WordAlignmentPlatformApiClient>(
    "WordAlignment",
    o =>
    {
        o.Address = new Uri(builder.Configuration.GetConnectionString("WordAlignmentPlatformApi")!);
    }
);

builder.Services.AddGrpc();

builder.Services.AddHostedService<BackgroundTaskService>();
builder.Services.AddSingleton<BackgroundTaskQueue>();

builder.Services.AddParallelCorpusPreprocessor();

builder.Services.AddHealthChecks().AddCheck("Live", () => HealthCheckResult.Healthy());

builder.Services.Configure<Bugsnag.Configuration>(builder.Configuration.GetSection("Bugsnag"));
builder.Services.AddBugsnag();

WebApplication app = builder.Build();

app.MapGrpcService<TranslationEngineServiceV1>();
app.MapGrpcService<WordAlignmentEngineServiceV1>();

app.MapGrpcService<HealthServiceV1>();

app.Run();
