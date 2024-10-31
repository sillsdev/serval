namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddWebhooks(this IServalBuilder builder)
    {
        builder.Services.AddHttpClient<WebhookJob>();
        builder.Services.AddScoped<IWebhookService, WebhookService>();
        return builder;
    }
}
