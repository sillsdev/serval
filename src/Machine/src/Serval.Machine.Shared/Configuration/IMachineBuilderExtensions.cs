using Serval.Translation.V1;
using Serval.WordAlignment.V1;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMachineBuilderExtensions
{
    public static IMachineBuilder AddServiceOptions(this IMachineBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<ServiceOptions>(config);
        return builder;
    }

    public static IMachineBuilder AddSmtTransferEngineOptions(this IMachineBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<SmtTransferEngineOptions>(config);
        return builder;
    }

    public static IMachineBuilder AddStatisticalEngineOptions(this IMachineBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<StatisticalEngineOptions>(config);
        return builder;
    }

    public static IMachineBuilder AddClearMLOptions(this IMachineBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<ClearMLOptions>(config);
        return builder;
    }

    public static IMachineBuilder AddDistributedReaderWriterLockOptions(
        this IMachineBuilder build,
        IConfiguration config
    )
    {
        build.Services.Configure<DistributedReaderWriterLockOptions>(config);
        return build;
    }

    public static IMachineBuilder AddSharedFileOptions(this IMachineBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<SharedFileOptions>(config);
        return builder;
    }

    public static IMachineBuilder AddBuildJobOptions(this IMachineBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<BuildJobOptions>(config);
        return builder;
    }

    public static IMachineBuilder AddThotSmtModel(this IMachineBuilder builder)
    {
        return builder.AddThotSmtModel(builder.Configuration.GetSection(ThotSmtModelOptions.Key));
    }

    public static IMachineBuilder AddThotSmtModel(this IMachineBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<ThotSmtModelOptions>(config);
        builder.Services.AddSingleton<ISmtModelFactory, ThotSmtModelFactory>();
        return builder;
    }

    public static IMachineBuilder AddWordAlignmentModel(this IMachineBuilder builder)
    {
        builder.Services.Configure<ThotWordAlignmentModelOptions>(
            builder.Configuration.GetSection(ThotWordAlignmentModelOptions.Key)
        );
        builder.Services.AddSingleton<IWordAlignmentModelFactory, ThotWordAlignmentModelFactory>();
        return builder;
    }

    public static IMachineBuilder AddTransferEngine(this IMachineBuilder builder)
    {
        builder.Services.AddSingleton<ITransferEngineFactory, TransferEngineFactory>();
        return builder;
    }

    public static IMachineBuilder AddUnigramTruecaser(this IMachineBuilder builder)
    {
        builder.Services.AddSingleton<ITruecaserFactory, UnigramTruecaserFactory>();
        return builder;
    }

    public static IMachineBuilder AddClearMLService(this IMachineBuilder builder)
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

    private static MongoStorageOptions GetMongoStorageOptions()
    {
        var mongoStorageOptions = new MongoStorageOptions
        {
            MigrationOptions = new MongoMigrationOptions
            {
                MigrationStrategy = new MigrateMongoMigrationStrategy(),
                BackupStrategy = new CollectionMongoBackupStrategy(),
            },
            CheckConnection = true,
            CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection,
        };
        return mongoStorageOptions;
    }

    public static IMachineBuilder AddMongoHangfireJobClient(this IMachineBuilder builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString("Hangfire");
        if (connectionString is null)
            throw new InvalidOperationException("Hangfire connection string is required");

        builder.Services.AddHangfire(c =>
            c.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMongoStorage(connectionString, GetMongoStorageOptions())
                .UseFilter(new AutomaticRetryAttribute { Attempts = 0 })
        );
        builder.Services.AddHealthChecks().AddHangfire();
        return builder;
    }

    public static IMachineBuilder AddHangfireJobServer(this IMachineBuilder builder)
    {
        IEnumerable<EngineType> engineTypes = (
            builder.Configuration.GetSection("TranslationEngines").Get<EngineType[]?>()
            ?? [EngineType.SmtTransfer, EngineType.Nmt]
        ).Concat(
            builder.Configuration.GetSection("WordAlignmentEngines").Get<EngineType[]?>() ?? [EngineType.Statistical]
        );
        var queues = new List<string>();
        foreach (EngineType engineType in engineTypes.Distinct())
        {
            switch (engineType)
            {
                case EngineType.SmtTransfer:
                    builder.Services.AddSingleton<SmtTransferEngineStateService>();
                    builder.AddThotSmtTransferEngine();
                    queues.Add("smt_transfer");
                    break;
                case EngineType.Nmt:
                    queues.Add("nmt");
                    break;
                case EngineType.Statistical:
                    builder.Services.AddSingleton<StatisticalEngineStateService>();
                    builder.AddThotStatisticalWordAlignment();
                    queues.Add("statistical");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(engineType.ToString());
            }
        }

        builder.Services.AddHangfireServer(o =>
        {
            o.Queues = queues.ToArray();
        });
        return builder;
    }

    public static IMachineBuilder AddMongoDataAccess(this IMachineBuilder builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString("Mongo");
        if (connectionString is null)
            throw new InvalidOperationException("Mongo connection string is required");
        builder.Services.AddMongoDataAccess(
            connectionString,
            "Serval.Machine.Shared.Models",
            o =>
            {
                o.AddRepository<TranslationEngine>(
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
                o.AddRepository<WordAlignmentEngine>(
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
                o.AddRepository<RWLock>("locks");
                o.AddRepository<TrainSegmentPair>(
                    "train_segment_pairs",
                    init: c =>
                        c.Indexes.CreateOrUpdateAsync(
                            new CreateIndexModel<TrainSegmentPair>(
                                Builders<TrainSegmentPair>.IndexKeys.Ascending(p => p.TranslationEngineRef)
                            )
                        )
                );
            }
        );
        builder.Services.AddHealthChecks().AddMongoDb(name: "Mongo");

        return builder;
    }

    public static IMachineBuilder AddMongoOutbox(this IMachineBuilder builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString("Mongo");
        if (connectionString is null)
            throw new InvalidOperationException("Mongo connection string is required");
        builder.Services.AddOutbox(builder.Configuration, x => x.UseMongo(connectionString));
        builder.Services.AddHealthChecks().AddOutbox();
        return builder;
    }

    public static IMachineBuilder AddMessageOutboxDeliveryService(this IMachineBuilder builder)
    {
        builder.Services.AddOutbox(x => x.UseDeliveryService());
        return builder;
    }

    public static IMachineBuilder AddServalTranslationPlatformService(this IMachineBuilder builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString("Serval");
        if (connectionString is null)
            throw new InvalidOperationException("Serval connection string is required");

        builder.Services.AddKeyedScoped<IPlatformService, ServalTranslationPlatformService>(EngineGroup.Translation);

        builder.Services.AddOutbox(x =>
        {
            x.AddConsumer<TranslationBuildStartedConsumer>();
            x.AddConsumer<TranslationBuildCompletedConsumer>();
            x.AddConsumer<TranslationBuildCanceledConsumer>();
            x.AddConsumer<TranslationBuildRestartingConsumer>();
            x.AddConsumer<TranslationBuildFaultedConsumer>();
            x.AddConsumer<TranslationIncrementEngineCorpusSizeConsumer>();
            x.AddConsumer<TranslationInsertPretranslationsConsumer>();
            x.AddConsumer<TranslationUpdateBuildExecutionDataConsumer>();
            x.AddConsumer<TranslationUpdateTargetQuoteConventionConsumer>();
        });

        builder
            .Services.AddGrpcClient<TranslationPlatformApi.TranslationPlatformApiClient>(o =>
            {
                o.Address = new Uri(connectionString);
            })
            .ConfigureChannel(o =>
            {
                o.MaxRetryAttempts = null;
                o.ServiceConfig = new ServiceConfig
                {
                    MethodConfigs =
                    {
                        new MethodConfig
                        {
                            Names = { MethodName.Default },
                            RetryPolicy = new Grpc.Net.Client.Configuration.RetryPolicy
                            {
                                MaxAttempts = 10,
                                InitialBackoff = TimeSpan.FromSeconds(1),
                                MaxBackoff = TimeSpan.FromSeconds(5),
                                BackoffMultiplier = 1.5,
                                RetryableStatusCodes = { StatusCode.Unavailable },
                            },
                        },
                        new MethodConfig
                        {
                            Names =
                            {
                                new MethodName
                                {
                                    Service = "serval.translation.v1.TranslationPlatformApi",
                                    Method = "UpdateTranslationBuildStatus",
                                },
                            },
                        },
                    },
                };
            });

        return builder;
    }

    public static IMachineBuilder AddServalWordAlignmentPlatformService(this IMachineBuilder builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString("Serval");
        if (connectionString is null)
            throw new InvalidOperationException("Serval connection string is required");

        builder.Services.AddKeyedScoped<IPlatformService, ServalWordAlignmentPlatformService>(
            EngineGroup.WordAlignment
        );

        builder.Services.AddOutbox(x =>
        {
            x.AddConsumer<WordAlignmentBuildStartedConsumer>();
            x.AddConsumer<WordAlignmentBuildCompletedConsumer>();
            x.AddConsumer<WordAlignmentBuildCanceledConsumer>();
            x.AddConsumer<WordAlignmentBuildRestartingConsumer>();
            x.AddConsumer<WordAlignmentBuildFaultedConsumer>();
            x.AddConsumer<WordAlignmentIncrementEngineCorpusSizeConsumer>();
            x.AddConsumer<WordAlignmentInsertWordAlignmentsConsumer>();
            x.AddConsumer<WordAlignmentUpdateBuildExecutionDataConsumer>();
        });

        builder
            .Services.AddGrpcClient<WordAlignmentPlatformApi.WordAlignmentPlatformApiClient>(o =>
            {
                o.Address = new Uri(connectionString);
            })
            .ConfigureChannel(o =>
            {
                o.MaxRetryAttempts = null;
                o.ServiceConfig = new ServiceConfig
                {
                    MethodConfigs =
                    {
                        new MethodConfig
                        {
                            Names = { MethodName.Default },
                            RetryPolicy = new Grpc.Net.Client.Configuration.RetryPolicy
                            {
                                MaxAttempts = 10,
                                InitialBackoff = TimeSpan.FromSeconds(1),
                                MaxBackoff = TimeSpan.FromSeconds(5),
                                BackoffMultiplier = 1.5,
                                RetryableStatusCodes = { StatusCode.Unavailable },
                            },
                        },
                        new MethodConfig
                        {
                            Names =
                            {
                                new MethodName
                                {
                                    Service = "serval.word_alignment.v1.WordAlignmentPlatformApi",
                                    Method = "UpdateWordAlignmentBuildStatus",
                                },
                            },
                        },
                    },
                };
            });

        return builder;
    }

    public static IMachineBuilder AddServalTranslationEngineService(this IMachineBuilder builder)
    {
        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<CancellationInterceptor>();
            options.Interceptors.Add<UnimplementedInterceptor>();
            options.Interceptors.Add<TimeoutInterceptor>();
            options.Interceptors.Add<FailedPreconditionInterceptor>();
            options.Interceptors.Add<NotFoundInterceptor>();
        });

        IEnumerable<EngineType> engineTypes =
            builder.Configuration.GetSection("TranslationEngines").Get<EngineType[]?>()
            ?? [EngineType.SmtTransfer, EngineType.Nmt];
        foreach (EngineType engineType in engineTypes.Distinct())
        {
            switch (engineType)
            {
                case EngineType.SmtTransfer:
                    builder.Services.AddSingleton<SmtTransferEngineStateService>();
                    builder.Services.AddHostedService<SmtTransferEngineCommitService>();
                    builder.AddThotSmtTransferEngine();
                    builder.Services.AddScoped<ITranslationEngineService, SmtTransferEngineService>();
                    break;
                case EngineType.Nmt:
                    builder.Services.AddScoped<ITranslationEngineService, NmtEngineService>();
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(engineType), (int)engineType, typeof(EngineType));
            }
        }

        return builder;
    }

    public static IMachineBuilder AddServalWordAlignmentEngineService(this IMachineBuilder builder)
    {
        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<CancellationInterceptor>();
            options.Interceptors.Add<UnimplementedInterceptor>();
            options.Interceptors.Add<TimeoutInterceptor>();
            options.Interceptors.Add<FailedPreconditionInterceptor>();
            options.Interceptors.Add<NotFoundInterceptor>();
        });

        IEnumerable<EngineType> engineTypes =
            builder.Configuration.GetSection("WordAlignmentEngines").Get<EngineType[]?>() ?? [EngineType.Statistical];

        foreach (EngineType engineType in engineTypes.Distinct())
        {
            switch (engineType)
            {
                case EngineType.Statistical:
                    builder.Services.AddSingleton<StatisticalEngineStateService>();
                    builder.AddThotStatisticalWordAlignment();
                    builder.Services.AddScoped<IWordAlignmentEngineService, StatisticalEngineService>();
                    builder.Services.AddHostedService<StatisticalEngineCommitService>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(engineType.ToString());
            }
        }

        return builder;
    }

    public static IMachineBuilder AddThotStatisticalWordAlignment(this IMachineBuilder builder)
    {
        builder.AddWordAlignmentModel();
        return builder;
    }

    public static IMachineBuilder AddThotSmtTransferEngine(this IMachineBuilder builder)
    {
        builder.AddThotSmtModel().AddTransferEngine().AddUnigramTruecaser();
        return builder;
    }

    public static IMachineBuilder AddBuildJobService(this IMachineBuilder builder)
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

    public static IMachineBuilder AddModelCleanupService(this IMachineBuilder builder)
    {
        builder.Services.AddHostedService<ModelCleanupService>();
        return builder;
    }
}
