WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpcClient<TranslationPlatformApi.TranslationPlatformApiClient>(o =>
{
    o.Address = new Uri(builder.Configuration.GetConnectionString("TranslationPlatformApi")!);
});
builder.Services.AddGrpc();

builder.Services.AddHostedService<BackgroundTaskService>();
builder.Services.AddSingleton<BackgroundTaskQueue>();

builder.Services.AddHealthChecks().AddCheck("Live", () => HealthCheckResult.Healthy());

builder.Services.Configure<Bugsnag.Configuration>(builder.Configuration.GetSection("Bugsnag"));
builder.Services.AddBugsnag();

WebApplication app = builder.Build();

app.MapGrpcService<TranslationEngineServiceV1>();
app.MapGrpcService<HealthServiceV1>();

app.Run();
