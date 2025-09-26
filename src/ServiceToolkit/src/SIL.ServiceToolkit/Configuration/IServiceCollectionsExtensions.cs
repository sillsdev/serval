namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddParallelCorpusPreprocessor(this IServiceCollection services)
    {
        services.TryAddSingleton<IParallelCorpusPreprocessingService, ParallelCorpusPreprocessingService>();
        services.TryAddSingleton<ITextCorpusService, TextCorpusService>();
        return services;
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

    public static IServiceCollection AddDiagnostics(this IServiceCollection services) =>
        services.AddHostedService<DiagnosticService>();
}
