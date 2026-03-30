using Serval.Translation.Contracts;
using Serval.WordAlignment.Contracts;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddHostedService<BackgroundTaskService>();
builder.Services.AddSingleton<BackgroundTaskQueue>();
builder.Services.AddSingleton<ITranslationEngineService, TranslationEngineService>();
builder.Services.AddSingleton<IWordAlignmentEngineService, WordAlignmentEngineService>();

builder.Services.AddParallelCorpusService();

builder.Services.AddHealthChecks().AddCheck("Live", () => HealthCheckResult.Healthy());

builder.Services.Configure<Bugsnag.Configuration>(builder.Configuration.GetSection("Bugsnag"));
builder.Services.AddBugsnag();
builder.Services.AddDiagnostics();

WebApplication app = builder.Build();

app.Run();
