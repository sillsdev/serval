using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddWordAlignmentRepositories(
        this IMongoDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<Engine>(
            "word_alignment.engines",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Engine>(Builders<Engine>.IndexKeys.Ascending(e => e.Owner))
                );
            }
        );
        configurator.AddRepository<Build>(
            "word_alignment.builds",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.EngineRef))
                )
        );
        configurator.AddRepository<WordAlignment>(
            "word_alignment.pretranslations",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<WordAlignment>(
                        Builders<WordAlignment>.IndexKeys.Ascending(pt => pt.ModelRevision)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<WordAlignment>(Builders<WordAlignment>.IndexKeys.Ascending(pt => pt.CorpusRef))
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<WordAlignment>(Builders<WordAlignment>.IndexKeys.Ascending(pt => pt.TextId))
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<WordAlignment>(
                        Builders<WordAlignment>
                            .IndexKeys.Ascending(pt => pt.EngineRef)
                            .Ascending(pt => pt.ModelRevision)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<WordAlignment>(
                        Builders<WordAlignment>
                            .IndexKeys.Ascending(pt => pt.EngineRef)
                            .Ascending(pt => pt.CorpusRef)
                            .Ascending(pt => pt.ModelRevision)
                            .Ascending(pt => pt.TextId)
                    )
                );
            }
        );
        return configurator;
    }
}
