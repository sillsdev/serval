namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddDataFiles(this IServalConfigurator configurator)
    {
        configurator.Services.AddHostedService<DeletedFileCleaner>();
        configurator.Services.AddScoped<DtoMapper>();
        configurator.Services.AddScoped<DataFileDeleter>();

        configurator.AddDataFilesDataAccess();

        configurator.AddHandlers(Assembly.GetExecutingAssembly());

        return configurator;
    }

    public static IServalConfigurator AddDataFilesDataAccess(this IServalConfigurator configurator)
    {
        configurator.DataAccess.AddRepository<DataFile>(
            "data_files.files",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<DataFile>(Builders<DataFile>.IndexKeys.Ascending(p => p.Owner))
                    ),
            ]
        );

        configurator.DataAccess.AddRepository<DeletedFile>(
            "data_files.deleted_files",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<DeletedFile>(Builders<DeletedFile>.IndexKeys.Ascending(p => p.DeletedAt))
                    ),
            ]
        );
        configurator.DataAccess.AddRepository<Corpus>(
            "corpora.corpus",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Corpus>(Builders<Corpus>.IndexKeys.Ascending(p => p.Owner))
                    ),
                // migrate by adding Name field
                c =>
                    c.UpdateManyAsync(
                        Builders<Corpus>.Filter.Exists(b => b.Name, false),
                        Builders<Corpus>.Update.Set(b => b.Name, null)
                    ),
            ]
        );
        return configurator;
    }
}
