using EchoTranslationEngine;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpcClient<TranslationPlatformApi.TranslationPlatformApiClient>(o =>
{
    o.Address = new Uri(builder.Configuration.GetConnectionString("TranslationPlatformApi"));
});
builder.Services.AddGrpc();

builder.Services.AddHostedService<BackgroundTaskService>();
builder.Services.AddSingleton<BackgroundTaskQueue>();

builder.Services.AddGrpcHealthChecks().AddCheck("Live", () => HealthCheckResult.Healthy());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapGrpcService<TranslationEngineServiceV1>();
app.MapGrpcHealthChecksService();

app.Run();
