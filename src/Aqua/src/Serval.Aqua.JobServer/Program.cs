using Hangfire;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder
    .Services.AddAqua(builder.Configuration)
    .AddMongoDataAccess()
    .AddMongoHangfireJobClient()
    .AddServalPlatformService()
    .AddAquaService();

builder.Services.AddHangfireServer();

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

app.Run();
