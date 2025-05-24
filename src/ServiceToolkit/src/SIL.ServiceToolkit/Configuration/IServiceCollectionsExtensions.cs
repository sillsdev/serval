namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddParallelCorpusPreprocessor(this IServiceCollection services)
    {
        services.TryAddSingleton<IParallelCorpusPreprocessingService, ParallelCorpusPreprocessingService>();
        services.TryAddSingleton<ICorpusService, CorpusService>();
        return services;
    }

    /// <summary>
    /// Add Bugsnag to your application. Configures the required bugsnag
    /// services and attaches the Bugsnag middleware to catch unhandled
    /// exceptions.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddBugsnag(this IServiceCollection services)
    {
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        return services
            .AddSingleton<IStartupFilter, BugsnagStartupFilter>()
            .AddScoped<Bugsnag.IClient, Bugsnag.Client>(context =>
            {
                IOptions<Bugsnag.Configuration> configuration = context.GetRequiredService<
                    IOptions<Bugsnag.Configuration>
                >();
                var client = new Bugsnag.Client(configuration.Value);
                return client;
            });
    }

    public static IServiceCollection AddFileSystem(this IServiceCollection services)
    {
        services.TryAddTransient<IFileSystem, FileSystem>();
        return services;
    }

    public static IServiceCollection AddOutbox(this IServiceCollection services, Action<IOutboxConfigurator> configure)
    {
        services.TryAddScoped<IOutboxService, OutboxService>();
        configure(new OutboxConfigurator(services));
        return services;
    }

    public static IServiceCollection AddOutbox(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IOutboxConfigurator> configure
    )
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.Key));
        services.TryAddScoped<IOutboxService, OutboxService>();
        configure(new OutboxConfigurator(services));
        return services;
    }
}
