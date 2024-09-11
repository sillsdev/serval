using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddMachine(builder.Configuration)
    .AddBuildJobService()
    .AddMongoDataAccess()
    .AddMongoHangfireJobClient()
    .AddHangfireJobServer()
    .AddServalPlatformService()
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

builder.Services.AddBugSnag(builder.Configuration);

var app = builder.Build();

app.Run();
