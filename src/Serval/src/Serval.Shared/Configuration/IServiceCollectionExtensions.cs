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
        services.AddScoped<IEventRouter, EventRouter>();

        services.Configure<DataFileOptions>(configuration.GetSection(DataFileOptions.Key));
        services.Configure<ApiOptions>(configuration.GetSection(ApiOptions.Key));

        string? mongoConnectionString = configuration.GetConnectionString("Mongo");
        if (mongoConnectionString is null)
            throw new InvalidOperationException("Mongo connection string not configured");
        IMongoDataAccessBuilder dataAccess = services.AddMongoDataAccess(mongoConnectionString, "Serval");
        services.AddHealthChecks().AddMongoDb(name: "Mongo");

        services.AddHangfire(c =>
            c.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMongoStorage(
                    configuration.GetConnectionString("Hangfire"),
                    new MongoStorageOptions
                    {
                        MigrationOptions = new MongoMigrationOptions
                        {
                            MigrationStrategy = new MigrateMongoMigrationStrategy(),
                            BackupStrategy = new CollectionMongoBackupStrategy(),
                        },
                        CheckConnection = true,
                        CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection,
                    }
                )
        );

        ServalConfigurator configurator = new(services, configuration, dataAccess);
        configure(configurator);

        services.AddHangfireServer(o => o.Queues = [.. configurator.JobQueues]);
        services.AddHealthChecks().AddCheck<HangfireHealthCheck>("Hangfire");

        return services;
    }
}
