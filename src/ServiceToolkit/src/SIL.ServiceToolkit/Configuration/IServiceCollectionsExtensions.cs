namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddParallelCorpusPreprocessor(this IServiceCollection services)
    {
        services.AddSingleton<IParallelCorpusPreprocessingService, ParallelCorpusPreprocessingService>();
        services.AddSingleton<ICorpusService, CorpusService>();
        return services;
    }
}
