using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddDataFilesRepositories(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<DataFile>(
            "data_files.files",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<DataFile>(Builders<DataFile>.IndexKeys.Ascending(p => p.Owner))
                )
        );

        configurator.AddRepository<DeletedFile>(
            "data_files.deleted_files",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<DeletedFile>(Builders<DeletedFile>.IndexKeys.Ascending(p => p.DeletedAt))
                )
        );
        return configurator;
    }

    public static IMongoDataAccessConfigurator AddCorporaRepository(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<Corpus>(
            "corpora.corpus",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Corpus>(Builders<Corpus>.IndexKeys.Ascending(p => p.Owner))
                )
        );
        return configurator;
    }
}
