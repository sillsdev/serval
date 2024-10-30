namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWebhooks(this IServalBuilder builder)
    {
        builder
            .Services.AddHttpClient<WebhookJob>()
            .AddTransientHttpErrorPolicy(b =>
                b.WaitAndRetryAsync(
                    6,
                    retryAttempt => TimeSpan.FromSeconds(new[] { 2, 4, 8, 12, 14, 16 }[retryAttempt]) // total 56 seconds, under the 1 minute https limit
                )
            );
        builder.Services.AddScoped<IWebhookService, WebhookService>();
        return builder;
    }
}
