using Serval.Assessment.V1;

namespace Microsoft.Extensions.DependencyInjection;

public static class IAquaBuilderExtensions
{
    public static IAquaBuilder AddAquaOptions(this IAquaBuilder builder, Action<AquaOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        return builder;
    }

    public static IAquaBuilder AddAquaOptions(this IAquaBuilder builder, IConfiguration config)
    {
        builder.Services.Configure<AquaOptions>(config);
        return builder;
    }

    public static IAquaBuilder AddAquaService(this IAquaBuilder builder, string? connectionString = null)
    {
        connectionString ??= builder.Configuration?.GetConnectionString("Aqua");
        if (connectionString is null)
            throw new InvalidOperationException("Aqua connection string is required");
        builder
            .Services.AddHttpClient("Aqua")
            .ConfigureHttpClient(httpClient => httpClient.BaseAddress = new Uri(connectionString))
            .AddHttpMessageHandler<AquaAccessTokenHandler>()
            // Add retry policy; fail after approx. 2 + 4 + 8 = 14 seconds
            .AddTransientHttpErrorPolicy(b =>
                b.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            );

        builder
            .Services.AddHttpClient("Aqua-NoAuth")
            .ConfigureHttpClient(httpClient => httpClient.BaseAddress = new Uri(connectionString));

        builder.Services.AddTransient<AquaAccessTokenHandler>();
        builder.Services.AddSingleton<IAquaService, AquaService>();
        builder.Services.AddSingleton<IAquaAuthService, AquaAuthService>();
        return builder;
    }

    public static IAquaBuilder AddMongoHangfireJobClient(this IAquaBuilder builder, string? connectionString = null)
    {
        connectionString ??= builder.Configuration?.GetConnectionString("Hangfire");
        if (connectionString is null)
            throw new InvalidOperationException("Hangfire connection string is required");

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

        builder.Services.AddHangfire(c =>
            c.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMongoStorage(connectionString, mongoStorageOptions)
                .UseFilter(new AutomaticRetryAttribute { Attempts = 0 })
        );
        builder.Services.AddHealthChecks().AddCheck<HangfireHealthCheck>(name: "Hangfire");
        return builder;
    }

    public static IAquaBuilder AddMemoryDataAccess(this IAquaBuilder builder)
    {
        builder.Services.AddMemoryDataAccess(o =>
        {
            o.AddRepository<Job>();
            o.AddRepository<Serval.Aqua.Shared.Models.Corpus>();
        });

        return builder;
    }

    public static IAquaBuilder AddMongoDataAccess(this IAquaBuilder builder, string? connectionString = null)
    {
        connectionString ??= builder.Configuration?.GetConnectionString("Mongo");
        if (connectionString is null)
            throw new InvalidOperationException("Mongo connection string is required");
        builder.Services.AddMongoDataAccess(
            connectionString,
            "Serval.Aqua.Shared.Models",
            o =>
            {
                o.AddRepository<Job>(
                    "jobs",
                    init: async c =>
                    {
                        await c.Indexes.CreateOrUpdateAsync(
                            new CreateIndexModel<Job>(Builders<Job>.IndexKeys.Ascending(b => b.StageState))
                        );
                    }
                );
                o.AddRepository<Serval.Aqua.Shared.Models.Corpus>(
                    "corpora",
                    init: c =>
                        c.Indexes.CreateOrUpdateAsync(
                            new CreateIndexModel<Serval.Aqua.Shared.Models.Corpus>(
                                Builders<Serval.Aqua.Shared.Models.Corpus>.IndexKeys.Ascending(c => c.Engines)
                            )
                        )
                );
            }
        );
        builder.Services.AddHealthChecks().AddMongoDb(connectionString, name: "Mongo");

        return builder;
    }

    public static IAquaBuilder AddServalPlatformService(this IAquaBuilder builder, string? connectionString = null)
    {
        connectionString ??= builder.Configuration?.GetConnectionString("Serval");
        if (connectionString is null)
            throw new InvalidOperationException("Serval connection string is required");

        builder.Services.AddScoped<IPlatformService, ServalPlatformService>();
        builder
            .Services.AddGrpcClient<AssessmentPlatformApi.AssessmentPlatformApiClient>(o =>
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
                                    Service = "serval.assessment.v1.AssessmentPlatformApi",
                                    Method = "UpdateJobStatus"
                                }
                            }
                        },
                    }
                };
            });

        return builder;
    }

    public static IAquaBuilder AddServalAssessmentEngineService(
        this IAquaBuilder builder,
        string? connectionString = null
    )
    {
        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<CancellationInterceptor>();
        });
        builder.AddServalPlatformService(connectionString);
        return builder;
    }

    public static IAquaBuilder AddAquaMonitorService(this IAquaBuilder builder)
    {
        builder.Services.AddHostedService<AquaMonitorService>();
        return builder;
    }
}
