using Serval.EngineApi.Translation;
using Serval.WordAlignment.V1;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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
builder.Services.AddSingleton<ITranslationEngine, TranslationEngineService>();

builder.Services.AddParallelCorpusService();

builder.Services.AddHealthChecks().AddCheck("Live", () => HealthCheckResult.Healthy());

builder.Services.Configure<Bugsnag.Configuration>(builder.Configuration.GetSection("Bugsnag"));
builder.Services.AddBugsnag();
builder.Services.AddDiagnostics();

WebApplication app = builder.Build();

app.MapGrpcService<WordAlignmentEngineServiceV1>();

app.MapGrpcService<HealthServiceV1>();

app.Run();
