namespace Microsoft.Extensions.DependencyInjection;

public static class IHealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddHangfire(this IHealthChecksBuilder builder, string name = "Hangfire")
    {
        builder.AddCheck<HangfireHealthCheck>(name);
        return builder;
    }

    public static IHealthChecksBuilder AddOutbox(this IHealthChecksBuilder builder, string name = "Outbox")
    {
        builder.AddCheck<OutboxHealthCheck>(name);
        return builder;
    }
}
