namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWebhooks(this IServalBuilder builder)
    {
        builder
            .Services.AddHttpClient<WebhookJob>()
            .AddTransientHttpErrorPolicy(b =>
                b.WaitAndRetryAsync(
                    7,
                    retryAttempt => TimeSpan.FromSeconds(2 * retryAttempt) // total 56, less than the 1 minute limit
                )
            );
        builder.Services.AddScoped<IWebhookService, WebhookService>();
        return builder;
    }
}
