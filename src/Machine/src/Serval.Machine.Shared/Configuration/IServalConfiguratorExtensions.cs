namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddMachineEngines(this IServalConfigurator configurator)
    {
        IConfiguration configuration = configurator.Configuration;
        IServiceCollection services = configurator.Services;

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

        configurator.AddBuildJobService();
        configurator.AddMachineDataAccess();
        configurator.AddClearMLService();

        configurator.AddTranslationEngines();
        configurator.AddWordAlignmentEngines();

        return configurator;
    }

    private static IServalConfigurator AddTranslationEngines(this IServalConfigurator configurator)
    {
        configurator.Services.AddKeyedScoped<IPlatformService, ServalTranslationPlatformService>(
            EngineGroup.Translation
        );

        // SMT Transfer Engine
        configurator.Services.AddSingleton<SmtTransferEngineStateService>();
        configurator.Services.AddHostedService<SmtTransferEngineCommitService>();
        configurator.Services.Configure<ThotSmtModelOptions>(
            configurator.Configuration.GetSection(ThotSmtModelOptions.Key)
        );
        configurator.Services.AddSingleton<ISmtModelFactory, ThotSmtModelFactory>();
        configurator.Services.AddSingleton<ITransferEngineFactory, TransferEngineFactory>();
        configurator.Services.AddSingleton<ITruecaserFactory, UnigramTruecaserFactory>();
        configurator.AddTranslationEngine<SmtTransferEngineService>(EngineType.SmtTransfer.ToString());
        configurator.JobQueues.Add(BuildJobQueues.SmtTransfer);

        // NMT Engine
        configurator.AddTranslationEngine<NmtEngineService>(EngineType.Nmt.ToString());
        configurator.JobQueues.Add(BuildJobQueues.Nmt);

        return configurator;
    }

    private static IServalConfigurator AddWordAlignmentEngines(this IServalConfigurator configurator)
    {
        configurator.Services.AddKeyedScoped<IPlatformService, ServalWordAlignmentPlatformService>(
            EngineGroup.WordAlignment
        );

        // Statistical Engine
        configurator.Services.AddSingleton<StatisticalEngineStateService>();
        configurator.Services.Configure<ThotWordAlignmentModelOptions>(
            configurator.Configuration.GetSection(ThotWordAlignmentModelOptions.Key)
        );
        configurator.Services.AddSingleton<IWordAlignmentModelFactory, ThotWordAlignmentModelFactory>();
        configurator.AddWordAlignmentEngine<StatisticalEngineService>(EngineType.Statistical.ToString());
        configurator.Services.AddHostedService<StatisticalEngineCommitService>();
        configurator.JobQueues.Add(BuildJobQueues.Statistical);

        return configurator;
    }

    private static IServiceCollection AddStartupTask(
        this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, Task> startupTask
    )
    {
        services.AddHostedService(sp => new StartupTask(sp, startupTask));
        return services;
    }

    private static IServalConfigurator AddClearMLService(this IServalConfigurator builder)
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

    public static IServalConfigurator AddMachineDataAccess(this IServalConfigurator configurator)
    {
        configurator.DataAccess.AddRepository<TranslationEngine>(
            "machine.translation_engines",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<TranslationEngine>(
                            Builders<TranslationEngine>.IndexKeys.Ascending(e => e.EngineId)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<TranslationEngine>(
                            Builders<TranslationEngine>.IndexKeys.Ascending(e => e.CurrentBuild!.BuildJobRunner)
                        )
                    ),
            ]
        );
        configurator.DataAccess.AddRepository<WordAlignmentEngine>(
            "machine.word_alignment_engines",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<WordAlignmentEngine>(
                            Builders<WordAlignmentEngine>.IndexKeys.Ascending(e => e.EngineId)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<WordAlignmentEngine>(
                            Builders<WordAlignmentEngine>.IndexKeys.Ascending(e => e.CurrentBuild!.BuildJobRunner)
                        )
                    ),
            ]
        );
        configurator.DataAccess.AddRepository<RWLock>("machine.locks");
        configurator.DataAccess.AddRepository<TrainSegmentPair>(
            "machine.train_segment_pairs",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<TrainSegmentPair>(
                            Builders<TrainSegmentPair>.IndexKeys.Ascending(p => p.TranslationEngineRef)
                        )
                    ),
            ]
        );
        return configurator;
    }

    private static IServalConfigurator AddBuildJobService(this IServalConfigurator configurator)
    {
        configurator.Services.AddScoped<IBuildJobService<TranslationEngine>, TranslationBuildJobService>();
        configurator.Services.AddScoped<IBuildJobService<WordAlignmentEngine>, BuildJobService<WordAlignmentEngine>>();

        configurator.Services.AddScoped<IBuildJobRunner, ClearMLBuildJobRunner>();
        configurator.Services.AddScoped<IClearMLBuildJobFactory, NmtClearMLBuildJobFactory>();
        configurator.Services.AddScoped<IClearMLBuildJobFactory, SmtTransferClearMLBuildJobFactory>();
        configurator.Services.AddScoped<IClearMLBuildJobFactory, StatisticalClearMLBuildJobFactory>();

        configurator.Services.AddSingleton<ClearMLMonitorService>();
        configurator.Services.AddSingleton<IClearMLQueueService>(x => x.GetRequiredService<ClearMLMonitorService>());
        configurator.Services.AddHostedService(p => p.GetRequiredService<ClearMLMonitorService>());

        configurator.Services.AddScoped<IBuildJobRunner, HangfireBuildJobRunner>();
        configurator.Services.AddScoped<IHangfireBuildJobFactory, NmtHangfireBuildJobFactory>();
        configurator.Services.AddScoped<IHangfireBuildJobFactory, SmtTransferHangfireBuildJobFactory>();
        configurator.Services.AddScoped<IHangfireBuildJobFactory, StatisticalHangfireBuildJobFactory>();

        var smtTransferEngineOptions = new SmtTransferEngineOptions();
        configurator.Configuration.GetSection(SmtTransferEngineOptions.Key).Bind(smtTransferEngineOptions);
        string? smtDriveLetter = Path.GetPathRoot(smtTransferEngineOptions.EnginesDir)?[..1];
        var statisticalEngineOptions = new StatisticalEngineOptions();
        configurator.Configuration.GetSection(StatisticalEngineOptions.Key).Bind(statisticalEngineOptions);
        string? statisticsDriveLetter = Path.GetPathRoot(statisticalEngineOptions.EnginesDir)?[..1];
        if (smtDriveLetter is null || statisticsDriveLetter is null)
            throw new InvalidOperationException("SMT Engine and Statistical directory is required");
        if (smtDriveLetter != statisticsDriveLetter)
            throw new InvalidOperationException("SMT Engine and Statistical directory must be on the same drive");
        // add health check for disk storage capacity
        configurator
            .Services.AddHealthChecks()
            .AddDiskStorageHealthCheck(
                x => x.AddDrive(smtDriveLetter, 1_000), // 1GB
                "SMT and Statistical Engine Storage Capacity",
                HealthStatus.Degraded
            );
        return configurator;
    }
}
