using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
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
            mapSetup: ms =>
            {
                ms.MapIdMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
                if (!BsonClassMap.IsClassMapRegistered(typeof(ParallelCorpus)))
                {
                    BsonClassMap.RegisterClassMap<ParallelCorpus>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
                    });
                }
                if (!BsonClassMap.IsClassMapRegistered(typeof(MonolingualCorpus)))
                {
                    BsonClassMap.RegisterClassMap<MonolingualCorpus>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
                    });
                }
                if (!BsonClassMap.IsClassMapRegistered(typeof(CorpusFile)))
                {
                    BsonClassMap.RegisterClassMap<CorpusFile>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(c => c.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
                    });
                }
            },
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
            mapSetup: ms =>
            {
                ms.MapIdMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
                ms.MapMember(m => m.EngineRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                if (!BsonClassMap.IsClassMapRegistered(typeof(TrainingCorpus)))
                {
                    BsonClassMap.RegisterClassMap<TrainingCorpus>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(m => m.ParallelCorpusRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                    });
                }
                if (!BsonClassMap.IsClassMapRegistered(typeof(WordAlignmentCorpus)))
                {
                    BsonClassMap.RegisterClassMap<WordAlignmentCorpus>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(m => m.ParallelCorpusRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                    });
                }
                if (!BsonClassMap.IsClassMapRegistered(typeof(ParallelCorpusFilter)))
                {
                    BsonClassMap.RegisterClassMap<ParallelCorpusFilter>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(m => m.CorpusRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                    });
                }
            },
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
            mapSetup: ms =>
            {
                ms.MapIdMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
                ms.MapMember(m => m.CorpusRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                ms.MapMember(m => m.EngineRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
            },
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
