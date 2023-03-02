using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddDataFilesRepositories(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<DataFile>(
            "files",
            init: c =>
                c.Indexes.CreateOrUpdate(
                    new CreateIndexModel<DataFile>(Builders<DataFile>.IndexKeys.Ascending(p => p.Owner))
                )
        );
        return configurator;
    }
}
