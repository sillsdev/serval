using Polly.Extensions.Http;
using Serval.Translation.V1;

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

    public static IMachineBuilder AddMessageOutboxOptions(this IMachineBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<MessageOutboxOptions>(config);
        return builder;
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

    public static IMachineBuilder AddServiceToolkitServices(this IMachineBuilder builder)
    {
        builder.Services.AddParallelCorpusPreprocessor();
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

    public static IMachineBuilder AddClearMLService(this IMachineBuilder builder, string? connectionString = null)
    {
        connectionString ??= builder.Configuration.GetConnectionString("ClearML");
        if (connectionString is null)
            throw new InvalidOperationException("ClearML connection string is required");

        var policy = Policy
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
                    var serviceProvider = builder.Services.BuildServiceProvider();
                    var logger = serviceProvider.GetService<ILogger<ClearMLService>>();
                    logger?.LogInformation(
                        "Retry {RetryAttempt} encountered an error. Waiting {Timespan} before next retry. Error: {ErrorMessage}",
                        retryAttempt,
                        timespan,
                        outcome.Exception?.Message
                    );
                    return Task.CompletedTask;
                }
            );

        builder
            .Services.AddHttpClient("ClearML")
            .ConfigureHttpClient(httpClient => httpClient.BaseAddress = new Uri(connectionString!))
            .AddPolicyHandler(policy);

        builder.Services.AddSingleton<IClearMLService, ClearMLService>();

        // workaround register satisfying the interface and as a hosted service.
        builder.Services.AddSingleton<IClearMLAuthenticationService, ClearMLAuthenticationService>();
        builder.Services.AddHostedService(p => p.GetRequiredService<IClearMLAuthenticationService>());

        builder
            .Services.AddHttpClient("ClearML-NoRetry")
            .ConfigureHttpClient(httpClient => httpClient.BaseAddress = new Uri(connectionString!));
        builder.Services.AddSingleton<ClearMLHealthCheck>();

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
                BackupStrategy = new CollectionMongoBackupStrategy()
            },
            CheckConnection = true,
            CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection,
        };
        return mongoStorageOptions;
    }

    public static IMachineBuilder AddMongoHangfireJobClient(
        this IMachineBuilder builder,
        string? connectionString = null
    )
    {
        connectionString ??= builder.Configuration.GetConnectionString("Hangfire");
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

    public static IMachineBuilder AddHangfireJobServer(
        this IMachineBuilder builder,
        IEnumerable<TranslationEngineType>? engineTypes = null
    )
    {
        engineTypes ??=
            builder.Configuration.GetSection("TranslationEngines").Get<TranslationEngineType[]?>()
            ?? [TranslationEngineType.SmtTransfer, TranslationEngineType.Nmt];
        var queues = new List<string>();
        foreach (TranslationEngineType engineType in engineTypes.Distinct())
        {
            switch (engineType)
            {
                case TranslationEngineType.SmtTransfer:
                    builder.Services.AddSingleton<SmtTransferEngineStateService>();
                    builder.AddThotSmtModel().AddTransferEngine().AddUnigramTruecaser();
                    queues.Add("smt_transfer");
                    break;
                case TranslationEngineType.Nmt:
                    queues.Add("nmt");
                    break;
            }
        }

        builder.Services.AddHangfireServer(o =>
        {
            o.Queues = queues.ToArray();
        });
        return builder;
    }

    public static IMachineBuilder AddMemoryDataAccess(this IMachineBuilder builder)
    {
        builder.Services.AddMemoryDataAccess(o =>
        {
            o.AddRepository<TranslationEngine>();
            o.AddRepository<RWLock>();
            o.AddRepository<TrainSegmentPair>();
            o.AddRepository<OutboxMessage>();
            o.AddRepository<Outbox>();
        });

        return builder;
    }

    public static IMachineBuilder AddMongoDataAccess(this IMachineBuilder builder, string? connectionString = null)
    {
        connectionString ??= builder.Configuration.GetConnectionString("Mongo");
        if (connectionString is null)
            throw new InvalidOperationException("Mongo connection string is required");
        builder.Services.AddMongoDataAccess(
            connectionString!,
            "Serval.Machine.Shared.Models",
            o =>
            {
                o.AddRepository<TranslationEngine>(
                    "translation_engines",
                    mapSetup: m => m.SetIgnoreExtraElements(true),
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
                o.AddRepository<OutboxMessage>(
                    "outbox_messages",
                    mapSetup: m => m.MapProperty(m => m.OutboxRef).SetSerializer(new StringSerializer())
                );
                o.AddRepository<Outbox>(
                    "outboxes",
                    mapSetup: m => m.MapIdProperty(o => o.Id).SetSerializer(new StringSerializer())
                );
            }
        );
        builder.Services.AddHealthChecks().AddMongoDb(connectionString!, name: "Mongo");

        return builder;
    }

    public static IMachineBuilder AddServalPlatformService(
        this IMachineBuilder builder,
        string? connectionString = null
    )
    {
        connectionString ??= builder.Configuration.GetConnectionString("Serval");
        if (connectionString is null)
            throw new InvalidOperationException("Serval connection string is required");

        builder.Services.AddScoped<IPlatformService, ServalPlatformService>();

        builder.Services.AddSingleton<IOutboxMessageHandler, ServalPlatformOutboxMessageHandler>();

        builder.Services.AddScoped<IMessageOutboxService, MessageOutboxService>();

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
                                RetryableStatusCodes = { StatusCode.Unavailable }
                            }
                        },
                        new MethodConfig
                        {
                            Names =
                            {
                                new MethodName
                                {
                                    Service = "serval.translation.v1.TranslationPlatformApi",
                                    Method = "UpdateBuildStatus"
                                }
                            }
                        },
                    }
                };
            });

        return builder;
    }

    public static IMachineBuilder AddServalTranslationEngineService(
        this IMachineBuilder builder,
        string? connectionString = null,
        IEnumerable<TranslationEngineType>? engineTypes = null
    )
    {
        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<CancellationInterceptor>();
            options.Interceptors.Add<UnimplementedInterceptor>();
            options.Interceptors.Add<TimeoutInterceptor>();
        });
        builder.AddServalPlatformService(connectionString);

        engineTypes ??=
            builder.Configuration.GetSection("TranslationEngines").Get<TranslationEngineType[]?>()
            ?? [TranslationEngineType.SmtTransfer, TranslationEngineType.Nmt];
        foreach (TranslationEngineType engineType in engineTypes.Distinct())
        {
            switch (engineType)
            {
                case TranslationEngineType.SmtTransfer:
                    builder.Services.AddSingleton<SmtTransferEngineStateService>();
                    builder.Services.AddHostedService<SmtTransferEngineCommitService>();
                    builder.AddThotSmtModel().AddTransferEngine().AddUnigramTruecaser();
                    builder.Services.AddScoped<ITranslationEngineService, SmtTransferEngineService>();
                    break;
                case TranslationEngineType.Nmt:
                    builder.Services.AddScoped<ITranslationEngineService, NmtEngineService>();
                    break;
            }
        }

        return builder;
    }

    public static IMachineBuilder AddBuildJobService(this IMachineBuilder builder, string? smtTransferEngineDir = null)
    {
        builder.Services.AddScoped<IBuildJobService, BuildJobService>();

        builder.Services.AddScoped<IBuildJobRunner, ClearMLBuildJobRunner>();
        builder.Services.AddScoped<IClearMLBuildJobFactory, NmtClearMLBuildJobFactory>();
        builder.Services.AddScoped<IClearMLBuildJobFactory, SmtTransferClearMLBuildJobFactory>();
        builder.Services.AddSingleton<ClearMLMonitorService>();
        builder.Services.AddSingleton<IClearMLQueueService>(x => x.GetRequiredService<ClearMLMonitorService>());
        builder.Services.AddHostedService(p => p.GetRequiredService<ClearMLMonitorService>());

        builder.Services.AddScoped<IBuildJobRunner, HangfireBuildJobRunner>();
        builder.Services.AddScoped<IHangfireBuildJobFactory, NmtHangfireBuildJobFactory>();
        builder.Services.AddScoped<IHangfireBuildJobFactory, SmtTransferHangfireBuildJobFactory>();

        if (smtTransferEngineDir is null)
        {
            var smtTransferEngineOptions = new SmtTransferEngineOptions();
            builder.Configuration.GetSection(SmtTransferEngineOptions.Key).Bind(smtTransferEngineOptions);
            smtTransferEngineDir = smtTransferEngineOptions.EnginesDir;
        }
        string? driveLetter = Path.GetPathRoot(smtTransferEngineDir)?[..1];
        if (driveLetter is null)
            throw new InvalidOperationException("SMT Engine directory is required");
        // add health check for disk storage capacity
        builder
            .Services.AddHealthChecks()
            .AddDiskStorageHealthCheck(
                x => x.AddDrive(driveLetter, 1_000), // 1GB
                "SMT Engine Storage Capacity",
                HealthStatus.Degraded
            );

        return builder;
    }

    public static IMachineBuilder AddModelCleanupService(this IMachineBuilder builder)
    {
        builder.Services.AddHostedService<ModelCleanupService>();
        return builder;
    }

    public static IMachineBuilder AddMessageOutboxDeliveryService(this IMachineBuilder builder)
    {
        builder.Services.AddHostedService<MessageOutboxDeliveryService>();
        return builder;
    }
}
