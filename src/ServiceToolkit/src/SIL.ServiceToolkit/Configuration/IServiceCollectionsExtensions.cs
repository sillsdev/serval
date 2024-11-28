namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddParallelCorpusPreprocessor(this IServiceCollection services)
    {
        services.AddSingleton<IParallelCorpusPreprocessingService, ParallelCorpusPreprocessingService>();
        services.AddSingleton<ICorpusService, CorpusService>();
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
                var configuration = context.GetService<IOptions<Bugsnag.Configuration>>();
                var client = new Bugsnag.Client(configuration!.Value);
                return client;
            });
    }
}
