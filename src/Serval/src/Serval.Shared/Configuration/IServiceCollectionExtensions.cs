namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddServal(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IServalConfigurator> configure
    )
    {
        services.AddTransient<IFileSystem, FileSystem>();
        services.AddSingleton<IParallelCorpusService, ParallelCorpusService>();
        services.AddSingleton<IBuildDiagnosticService, BuildDiagnosticService>();
        services.AddScoped<IEventRouter, EventRouter>();

        services.Configure<DataFileOptions>(configuration.GetSection(DataFileOptions.Key));
        services.Configure<ApiOptions>(configuration.GetSection(ApiOptions.Key));

        string? mongoConnectionString = configuration.GetConnectionString("Mongo");
        if (mongoConnectionString is null)
            throw new InvalidOperationException("Mongo connection string not configured");
        IMongoDataAccessBuilder dataAccess = services.AddMongoDataAccess(mongoConnectionString, "Serval");
        services.AddHealthChecks().AddMongoDb(name: "Mongo");

        ServalConfigurator configurator = new(services, configuration, dataAccess);
        configure(configurator);

        services.AddStartupTask(
            (sp, ct) =>
            {
                var fileSystem = sp.GetRequiredService<IFileSystem>();
                var dataFileOptions = sp.GetRequiredService<IOptionsSnapshot<DataFileOptions>>();
                fileSystem.CreateDirectory(dataFileOptions.Value.FilesDirectory);
                return Task.CompletedTask;
            }
        );

        return services;
    }

    public static IServiceCollection AddStartupTask(
        this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, Task> startupTask
    )
    {
        services.AddHostedService(sp => new StartupTask(sp, startupTask));
        return services;
    }
}
