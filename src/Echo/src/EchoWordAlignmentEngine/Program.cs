WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpcClient<WordAlignmentPlatformApi.WordAlignmentPlatformApiClient>(o =>
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

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapGrpcService<WordAlignmentEngineServiceV1>();
app.MapGrpcService<HealthServiceV1>();

app.Run();
