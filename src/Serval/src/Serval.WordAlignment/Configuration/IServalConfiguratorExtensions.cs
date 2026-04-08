namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddWordAlignment(this IServalConfigurator configurator)
    {
        configurator.Services.AddScoped<IBuildService, BuildService>();
        configurator.Services.AddScoped<IWordAlignmentService, WordAlignmentService>();
        configurator.Services.AddScoped<IEngineService, EngineService>();
        configurator.Services.AddScoped<IEngineServiceFactory, EngineServiceFactory>();
        configurator.Services.AddScoped<IWordAlignmentPlatformService, PlatformService>();

        configurator.AddWordAlignmentDataAccess();

        configurator.AddHandlers(Assembly.GetExecutingAssembly());

        return configurator;
    }

    public static IServalConfigurator AddWordAlignmentDataAccess(this IServalConfigurator configurator)
    {
        configurator.DataAccess.AddRepository<Engine>(
            "word_alignment.engines",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Engine>(Builders<Engine>.IndexKeys.Ascending(e => e.Owner))
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Engine>(Builders<Engine>.IndexKeys.Ascending(e => e.DateCreated))
                    ),
            ]
        );
        configurator.DataAccess.AddRepository<Build>(
            "word_alignment.builds",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.EngineRef))
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.DateCreated))
                    ),
                // migrate the percentCompleted field to the progress field
                c =>
                    c.UpdateManyAsync(
                        Builders<Build>.Filter.And(
                            Builders<Build>.Filter.Exists("percentCompleted"),
                            Builders<Build>.Filter.Exists(b => b.Progress, false)
                        ),
                        new BsonDocument("$rename", new BsonDocument("percentCompleted", "progress"))
                    ),
            ]
        );
        configurator.DataAccess.AddRepository<WordAlignment>(
            "word_alignment.word_alignments",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<WordAlignment>(
                            Builders<WordAlignment>.IndexKeys.Ascending(pt => pt.ModelRevision)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<WordAlignment>(
                            Builders<WordAlignment>.IndexKeys.Ascending(pt => pt.CorpusRef)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<WordAlignment>(
                            Builders<WordAlignment>.IndexKeys.Ascending(pt => pt.TextId)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<WordAlignment>(
                            Builders<WordAlignment>
                                .IndexKeys.Ascending(pt => pt.EngineRef)
                                .Ascending(pt => pt.ModelRevision)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<WordAlignment>(
                            Builders<WordAlignment>
                                .IndexKeys.Ascending(pt => pt.EngineRef)
                                .Ascending(pt => pt.CorpusRef)
                                .Ascending(pt => pt.ModelRevision)
                                .Ascending(pt => pt.TextId)
                        )
                    ),
            ]
        );

        return configurator;
    }
}
