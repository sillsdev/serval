namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IMachineBuilder AddMachine(this IServiceCollection services, IConfiguration configuration)
    {
        if (!Sldr.IsInitialized)
            Sldr.Initialize();

        services.AddMemoryCache();
        services.AddSingleton<ISharedFileService, SharedFileService>();
        services.AddHealthChecks().AddCheck<S3HealthCheck>("S3 Bucket");

        services.AddSingleton<ILanguageTagService, LanguageTagService>();

        services.AddScoped<IDistributedReaderWriterLockFactory, DistributedReaderWriterLockFactory>();
        services.AddStartupTask(
            (sp, cancellationToken) =>
                sp.GetRequiredService<IDistributedReaderWriterLockFactory>().InitAsync(cancellationToken)
        );

        var builder = new MachineBuilder(services, configuration);

        builder.AddServiceOptions(configuration.GetSection(ServiceOptions.Key));
        builder.AddSharedFileOptions(configuration.GetSection(SharedFileOptions.Key));
        builder.AddSmtTransferEngineOptions(configuration.GetSection(SmtTransferEngineOptions.Key));
        builder.AddStatisticalEngineOptions(configuration.GetSection(StatisticalEngineOptions.Key));
        builder.AddClearMLOptions(configuration.GetSection(ClearMLOptions.Key));
        builder.AddDistributedReaderWriterLockOptions(configuration.GetSection(DistributedReaderWriterLockOptions.Key));
        builder.AddBuildJobOptions(configuration.GetSection(BuildJobOptions.Key));

        builder.AddBuildJobService();
        builder.AddMongoDataAccess();
        builder.AddMongoHangfireJobClient();
        builder.AddClearMLService();

        return builder;
    }

    private static IServiceCollection AddStartupTask(
        this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, Task> startupTask
    )
    {
        services.AddHostedService(sp => new StartupTask(sp, startupTask));
        return services;
    }
}
