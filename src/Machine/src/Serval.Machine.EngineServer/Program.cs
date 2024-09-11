using Bugsnag.AspNet.Core;
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

app.UseHttpsRedirection();

app.MapServalTranslationEngineService();
app.MapHangfireDashboard();

app.Run();
