namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IMachineBuilder AddMachine(this IServiceCollection services, IConfiguration configuration)
    {
        if (!Sldr.IsInitialized)
            Sldr.Initialize();

        services.AddSingleton<ISharedFileService, SharedFileService>();
        services.AddSingleton<S3HealthCheck>();
        services.AddHealthChecks().AddCheck<S3HealthCheck>("S3 Bucket");

        services.AddSingleton<ILanguageTagService, LanguageTagService>();
        services.AddTransient<IFileSystem, FileSystem>();

        services.AddScoped<IDistributedReaderWriterLockFactory, DistributedReaderWriterLockFactory>();
        services.AddStartupTask(
            (sp, cancellationToken) =>
                sp.GetRequiredService<IDistributedReaderWriterLockFactory>().InitAsync(cancellationToken)
        );
        services.AddParallelCorpusPreprocessor();

        var builder = new MachineBuilder(services, configuration);
        builder.AddServiceOptions(configuration.GetSection(ServiceOptions.Key));
        builder.AddSharedFileOptions(configuration.GetSection(SharedFileOptions.Key));
        builder.AddSmtTransferEngineOptions(configuration.GetSection(SmtTransferEngineOptions.Key));
        builder.AddWordAlignmentEngineOptions(configuration.GetSection(StatisticalWordAlignmentEngineOptions.Key));
        builder.AddClearMLOptions(configuration.GetSection(ClearMLOptions.Key));
        builder.AddDistributedReaderWriterLockOptions(configuration.GetSection(DistributedReaderWriterLockOptions.Key));
        builder.AddBuildJobOptions(configuration.GetSection(BuildJobOptions.Key));
        builder.AddMessageOutboxOptions(configuration.GetSection(MessageOutboxOptions.Key));
        return builder;
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
