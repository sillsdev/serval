namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddMachineEngines(this IServalBuilder builder)
    {
        IConfiguration configuration = builder.Configuration;
        IServiceCollection services = builder.Services;

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

        services.Configure<ServiceOptions>(configuration.GetSection(ServiceOptions.Key));
        services.Configure<SharedFileOptions>(configuration.GetSection(SharedFileOptions.Key));
        services.Configure<SmtTransferEngineOptions>(configuration.GetSection(SmtTransferEngineOptions.Key));
        services.Configure<StatisticalEngineOptions>(configuration.GetSection(StatisticalEngineOptions.Key));
        services.Configure<ClearMLOptions>(configuration.GetSection(ClearMLOptions.Key));
        services.Configure<DistributedReaderWriterLockOptions>(
            configuration.GetSection(DistributedReaderWriterLockOptions.Key)
        );
        services.Configure<BuildJobOptions>(configuration.GetSection(BuildJobOptions.Key));

        services.AddHostedService<ModelCleanupService>();

        builder.AddBuildJobService();
        builder.AddMongoDataAccess();
        builder.AddClearMLService();

        builder.AddTranslationEngines();
        builder.AddWordAlignmentEngines();

        return builder;
    }

    private static IServalBuilder AddTranslationEngines(this IServalBuilder builder)
    {
        builder.Services.AddKeyedScoped<IPlatformService, ServalTranslationPlatformService>(EngineGroup.Translation);

        // SMT Transfer Engine
        builder.Services.AddSingleton<SmtTransferEngineStateService>();
        builder.Services.AddHostedService<SmtTransferEngineCommitService>();
        builder.Services.Configure<ThotSmtModelOptions>(builder.Configuration.GetSection(ThotSmtModelOptions.Key));
        builder.Services.AddSingleton<ISmtModelFactory, ThotSmtModelFactory>();
        builder.Services.AddSingleton<ITransferEngineFactory, TransferEngineFactory>();
        builder.Services.AddSingleton<ITruecaserFactory, UnigramTruecaserFactory>();
        builder.AddTranslationEngine<SmtTransferEngineService>(EngineType.SmtTransfer.ToString());
        builder.JobQueues.Add(EngineType.SmtTransfer.ToString());

        // NMT Engine
        builder.AddTranslationEngine<NmtEngineService>(EngineType.Nmt.ToString());
        builder.JobQueues.Add(EngineType.Nmt.ToString());

        return builder;
    }

    private static IServalBuilder AddWordAlignmentEngines(this IServalBuilder builder)
    {
        builder.Services.AddKeyedScoped<IPlatformService, ServalWordAlignmentPlatformService>(
            EngineGroup.WordAlignment
        );

        // Statistical Engine
        builder.Services.AddSingleton<StatisticalEngineStateService>();
        builder.Services.Configure<ThotWordAlignmentModelOptions>(
            builder.Configuration.GetSection(ThotWordAlignmentModelOptions.Key)
        );
        builder.Services.AddSingleton<IWordAlignmentModelFactory, ThotWordAlignmentModelFactory>();
        builder.AddWordAlignmentEngine<StatisticalEngineService>(EngineType.Statistical.ToString());
        builder.Services.AddHostedService<StatisticalEngineCommitService>();
        builder.JobQueues.Add(EngineType.Statistical.ToString());

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

    private static IServalBuilder AddClearMLService(this IServalBuilder builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString("ClearML");
        if (connectionString is null)
            throw new InvalidOperationException("ClearML connection string is required");

        builder
            .Services.AddHttpClient("ClearML")
            .ConfigureHttpClient(httpClient => httpClient.BaseAddress = new Uri(connectionString!))
            .AddPolicyHandler(
                (serviceProvider, _) =>
                    Policy
                        .Handle<HttpRequestException>()
                        .OrTransientHttpStatusCode()
                        .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                        .WaitAndRetryAsync(
                            7,
                            retryAttempt => TimeSpan.FromSeconds(2 * retryAttempt), // total 56, less than the 1 minute limit
                            onRetryAsync: (outcome, timespan, retryAttempt, context) =>
                            {
                                if (retryAttempt < 3)
                                    return Task.CompletedTask;
                                // Log the retry attempt
                                var logger = serviceProvider.GetRequiredService<ILogger<ClearMLService>>();
                                logger.LogInformation(
                                    "Retry {RetryAttempt} encountered an error. Waiting {Timespan} before next retry. Error: {ErrorMessage}",
                                    retryAttempt,
                                    timespan,
                                    outcome.Exception?.Message
                                );
                                return Task.CompletedTask;
                            }
                        )
            );

        builder.Services.AddSingleton<IClearMLService, ClearMLService>();

        // workaround register satisfying the interface and as a hosted service.
        builder.Services.AddSingleton<IClearMLAuthenticationService, ClearMLAuthenticationService>();
        builder.Services.AddHostedService(p => p.GetRequiredService<IClearMLAuthenticationService>());

        builder
            .Services.AddHttpClient("ClearML-NoRetry")
            .ConfigureHttpClient(httpClient => httpClient.BaseAddress = new Uri(connectionString!));

        builder.Services.AddHealthChecks().AddCheck<ClearMLHealthCheck>("ClearML Health Check");
        return builder;
    }

    private static IServalBuilder AddMongoDataAccess(this IServalBuilder builder)
    {
        string? databaseName = builder.Configuration.GetConnectionString("MachineDatabase");
        if (databaseName is null)
            throw new InvalidOperationException("Machine database not configured.");
        builder.DataAccess.AddRepository<TranslationEngine>(
            databaseName,
            "translation_engines",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<TranslationEngine>(
                        Builders<TranslationEngine>.IndexKeys.Ascending(e => e.EngineId)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<TranslationEngine>(
                        Builders<TranslationEngine>.IndexKeys.Ascending(e => e.CurrentBuild!.BuildJobRunner)
                    )
                );
            }
        );
        builder.DataAccess.AddRepository<WordAlignmentEngine>(
            databaseName,
            "word_alignment_engines",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<WordAlignmentEngine>(
                        Builders<WordAlignmentEngine>.IndexKeys.Ascending(e => e.EngineId)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<WordAlignmentEngine>(
                        Builders<WordAlignmentEngine>.IndexKeys.Ascending(e => e.CurrentBuild!.BuildJobRunner)
                    )
                );
            }
        );
        builder.DataAccess.AddRepository<RWLock>(databaseName, "locks");
        builder.DataAccess.AddRepository<TrainSegmentPair>(
            databaseName,
            "train_segment_pairs",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<TrainSegmentPair>(
                        Builders<TrainSegmentPair>.IndexKeys.Ascending(p => p.TranslationEngineRef)
                    )
                )
        );
        return builder;
    }

    private static IServalBuilder AddBuildJobService(this IServalBuilder builder)
    {
        builder.Services.AddScoped<IBuildJobService<TranslationEngine>, TranslationBuildJobService>();
        builder.Services.AddScoped<IBuildJobService<WordAlignmentEngine>, BuildJobService<WordAlignmentEngine>>();

        builder.Services.AddScoped<IBuildJobRunner, ClearMLBuildJobRunner>();
        builder.Services.AddScoped<IClearMLBuildJobFactory, NmtClearMLBuildJobFactory>();
        builder.Services.AddScoped<IClearMLBuildJobFactory, SmtTransferClearMLBuildJobFactory>();
        builder.Services.AddScoped<IClearMLBuildJobFactory, StatisticalClearMLBuildJobFactory>();

        builder.Services.AddSingleton<ClearMLMonitorService>();
        builder.Services.AddSingleton<IClearMLQueueService>(x => x.GetRequiredService<ClearMLMonitorService>());
        builder.Services.AddHostedService(p => p.GetRequiredService<ClearMLMonitorService>());

        builder.Services.AddScoped<IBuildJobRunner, HangfireBuildJobRunner>();
        builder.Services.AddScoped<IHangfireBuildJobFactory, NmtHangfireBuildJobFactory>();
        builder.Services.AddScoped<IHangfireBuildJobFactory, SmtTransferHangfireBuildJobFactory>();
        builder.Services.AddScoped<IHangfireBuildJobFactory, StatisticalHangfireBuildJobFactory>();

        var smtTransferEngineOptions = new SmtTransferEngineOptions();
        builder.Configuration.GetSection(SmtTransferEngineOptions.Key).Bind(smtTransferEngineOptions);
        string? smtDriveLetter = Path.GetPathRoot(smtTransferEngineOptions.EnginesDir)?[..1];
        var statisticalEngineOptions = new StatisticalEngineOptions();
        builder.Configuration.GetSection(StatisticalEngineOptions.Key).Bind(statisticalEngineOptions);
        string? statisticsDriveLetter = Path.GetPathRoot(statisticalEngineOptions.EnginesDir)?[..1];
        if (smtDriveLetter is null || statisticsDriveLetter is null)
            throw new InvalidOperationException("SMT Engine and Statistical directory is required");
        if (smtDriveLetter != statisticsDriveLetter)
            throw new InvalidOperationException("SMT Engine and Statistical directory must be on the same drive");
        // add health check for disk storage capacity
        builder
            .Services.AddHealthChecks()
            .AddDiskStorageHealthCheck(
                x => x.AddDrive(smtDriveLetter, 1_000), // 1GB
                "SMT and Statistical Engine Storage Capacity",
                HealthStatus.Degraded
            );
        return builder;
    }
}
