namespace Microsoft.Extensions.DependencyInjection;

public static class IServalBuilderExtensions
{
    public static IServalBuilder AddDataFiles(this IServalBuilder builder)
    {
        builder.Services.AddScoped<IDataFileService, DataFileService>();
        builder.Services.AddHostedService<DeletedFileCleaner>();

        builder.Services.AddScoped<ICorpusService, CorpusService>();

        builder.AddMongoDataAccess();

        builder.AddHandlers(Assembly.GetExecutingAssembly());

        return builder;
    }

    private static IServalBuilder AddMongoDataAccess(this IServalBuilder builder)
    {
        string databaseName = builder.GetDatabaseName();
        builder.DataAccess.AddRepository<DataFile>(
            databaseName,
            "data_files.files",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<DataFile>(Builders<DataFile>.IndexKeys.Ascending(p => p.Owner))
                )
        );

        builder.DataAccess.AddRepository<DeletedFile>(
            databaseName,
            "data_files.deleted_files",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<DeletedFile>(Builders<DeletedFile>.IndexKeys.Ascending(p => p.DeletedAt))
                )
        );
        builder.DataAccess.AddRepository<Corpus>(
            databaseName,
            "corpora.corpus",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Corpus>(Builders<Corpus>.IndexKeys.Ascending(p => p.Owner))
                );
                // migrate by adding Name field
                await c.UpdateManyAsync(
                    Builders<Corpus>.Filter.Exists(b => b.Name, false),
                    Builders<Corpus>.Update.Set(b => b.Name, null)
                );
            }
        );
        return builder;
    }
}
