namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddParallelCorpusService(this IServiceCollection services)
    {
        services.TryAddSingleton<IParallelCorpusService, ParallelCorpusService>();
        return services;
    }

    public static IServiceCollection AddFileSystem(this IServiceCollection services)
    {
        services.TryAddTransient<IFileSystem, FileSystem>();
        return services;
    }

    public static IServiceCollection AddDiagnostics(this IServiceCollection services) =>
        services.AddHostedService<DiagnosticService>();
}
