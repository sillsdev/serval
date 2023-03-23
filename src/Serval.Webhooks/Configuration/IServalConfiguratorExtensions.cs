using Polly;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddWebhooks(this IServalConfigurator configurator)
    {
        configurator.Services.AddAutoMapper(Assembly.GetExecutingAssembly());

        configurator.Services
            .AddHttpClient<WebhookJob>()
            .AddTransientHttpErrorPolicy(
                b => b.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            );
        configurator.Services.AddScoped<IWebhookService, WebhookService>();
        return configurator;
    }
}
