using MongoDB.Bson;
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
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Engine>(Builders<Engine>.IndexKeys.Ascending(e => e.DateCreated))
                );
            }
        );
        configurator.AddRepository<Build>(
            "word_alignment.builds",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.EngineRef))
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.DateCreated))
                );
                // migrate the percentCompleted field to the progress field
                await c.UpdateManyAsync(
                    Builders<Build>.Filter.And(
                        Builders<Build>.Filter.Exists("percentCompleted"),
                        Builders<Build>.Filter.Exists(b => b.Progress, false)
                    ),
                    new BsonDocument("$rename", new BsonDocument("percentCompleted", "progress"))
                );
            }
        );
        configurator.AddRepository<WordAlignment>(
            "word_alignment.word_alignments",
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
