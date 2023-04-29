namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWebhooks(this IServalBuilder builder)
    {
        builder.Services
            .AddHttpClient<WebhookJob>()
            .AddTransientHttpErrorPolicy(
                b => b.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            );
        builder.Services.AddScoped<IWebhookService, WebhookService>();
        return builder;
    }
}
