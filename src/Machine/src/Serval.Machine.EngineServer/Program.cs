using Hangfire;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder
    .Services.AddMachine(builder.Configuration)
    .AddBuildJobService()
    .AddMongoDataAccess()
    .AddMongoHangfireJobClient()
    .AddServalTranslationEngineService()
    .AddServalWordAlignmentEngineService()
    .AddServalTranslationPlatformService()
    .AddServalWordAlignmentPlatformService()
    .AddModelCleanupService()
    .AddMessageOutboxDeliveryService()
    .AddClearMLService();

if (builder.Environment.IsDevelopment())
{
    builder
        .Services.AddOpenTelemetry()
        .WithTracing(builder =>
        {
            builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
                .AddConsoleExporter();
        });
}

builder.Services.Configure<Bugsnag.Configuration>(builder.Configuration.GetSection("Bugsnag"));
builder.Services.AddBugsnag();

var app = builder.Build();

app.MapServalTranslationEngineService();
app.MapServalWordAlignmentEngineService();
app.MapHangfireDashboard();

app.Run();
