namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServalBuilder AddServal(this IServiceCollection services, IConfiguration? config = null)
    {
        services.AddScoped<ICorpusService, CorpusService>();
        services
            .AddHttpClient<IWebhookService, WebhookService>()
            .AddTransientHttpErrorPolicy(
                b => b.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            );
        services.AddScoped<IPretranslationService, PretranslationService>();
        services.AddScoped<IBuildService, BuildService>();

        services.AddAutoMapper(Assembly.GetExecutingAssembly());

        var builder = new ServalBuilder(services, config).AddOptions();
        return builder;
    }
}
