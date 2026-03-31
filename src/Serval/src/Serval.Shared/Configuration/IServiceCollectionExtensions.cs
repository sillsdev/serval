namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    public static IServalBuilder AddServal(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddTransient<IFileSystem, FileSystem>();
        services.TryAddSingleton<IParallelCorpusService, ParallelCorpusService>();

        services.Configure<DataFileOptions>(configuration.GetSection(DataFileOptions.Key));
        services.Configure<ApiOptions>(configuration.GetSection(ApiOptions.Key));

        string? mongoConnectionString = configuration.GetConnectionString("Mongo");
        if (mongoConnectionString is null)
            throw new InvalidOperationException("Mongo connection string not configured");
        IMongoDataAccessBuilder dataAccess = services.AddMongoDataAccess(mongoConnectionString, "Serval");
        services.AddHealthChecks().AddMongoDb(name: "Mongo");

        return new ServalBuilder(services, configuration, dataAccess);
    }
}
