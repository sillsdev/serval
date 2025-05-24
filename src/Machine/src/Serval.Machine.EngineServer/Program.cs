using Hangfire;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder
    .Services.AddMachine(builder.Configuration)
    .AddServalTranslationEngineService()
    .AddServalWordAlignmentEngineService()
    .AddServalTranslationPlatformService()
    .AddServalWordAlignmentPlatformService()
    .AddModelCleanupService()
    .AddMessageOutboxDeliveryService();

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

WebApplication app = builder.Build();

app.MapServalTranslationEngineService();
app.MapServalWordAlignmentEngineService();
app.MapHangfireDashboard();

app.Run();
