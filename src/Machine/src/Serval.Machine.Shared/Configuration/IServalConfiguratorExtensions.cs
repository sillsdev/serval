namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddMachine(this IServalConfigurator configurator)
    {
        IConfiguration configuration = configurator.Configuration;
        IServiceCollection services = configurator.Services;

        if (!Sldr.IsInitialized)
            Sldr.Initialize();

        services.AddMemoryCache();
        services.AddSingleton<ISharedFileService, SharedFileService>();
        services.AddHealthChecks().AddCheck<S3HealthCheck>("S3 Bucket");

        services.Configure<ServiceOptions>(configuration.GetSection(ServiceOptions.Key));
        services.Configure<SharedFileOptions>(configuration.GetSection(SharedFileOptions.Key));
        services.Configure<ClearMLOptions>(configuration.GetSection(ClearMLOptions.Key));
        services.Configure<BuildJobOptions>(configuration.GetSection(BuildJobOptions.Key));

        configurator.AddClearMLService();

        return configurator;
    }

    private static IServalConfigurator AddClearMLService(this IServalConfigurator builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString("ClearML");
        if (connectionString is null)
            throw new InvalidOperationException("ClearML connection string is required");

        builder
            .Services.AddHttpClient("ClearML")
            .ConfigureHttpClient(httpClient => httpClient.BaseAddress = new Uri(connectionString!))
            .AddResilienceHandler(
                "ClearML",
                (builder, context) =>
                {
                    var logger = context.ServiceProvider.GetRequiredService<ILogger<ClearMLService>>();
                    builder.AddRetry(
                        new HttpRetryStrategyOptions
                        {
                            MaxRetryAttempts = 7,
                            Delay = TimeSpan.FromSeconds(2),
                            BackoffType = DelayBackoffType.Linear,
                            UseJitter = false,
                            ShouldRetryAfterHeader = false,
                            OnRetry = args =>
                            {
                                int retryAttempt = args.AttemptNumber + 1;
                                if (retryAttempt >= 3)
                                {
                                    logger.LogInformation(
                                        "Retry {RetryAttempt} encountered an error. Waiting {Timespan} before next retry. Error: {ErrorMessage}",
                                        retryAttempt,
                                        args.RetryDelay,
                                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString()
                                    );
                                }

                                return default;
                            },
                        }
                    );
                }
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
}
