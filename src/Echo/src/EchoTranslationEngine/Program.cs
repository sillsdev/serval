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

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapGrpcService<TranslationEngineServiceV1>();

app.Run();