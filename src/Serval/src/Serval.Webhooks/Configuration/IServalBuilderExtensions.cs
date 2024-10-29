namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWebhooks(this IServalBuilder builder)
    {
        builder
            .Services.AddHttpClient<WebhookJob>()
            // Add retry policy; fail after approx. 1.5 + 2.25 ... 1.5^6 ~= 31 seconds
            .AddTransientHttpErrorPolicy(b =>
                b.WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(1.5, retryAttempt)))
            );
        builder.Services.AddScoped<IWebhookService, WebhookService>();
        return builder;
    }
}
