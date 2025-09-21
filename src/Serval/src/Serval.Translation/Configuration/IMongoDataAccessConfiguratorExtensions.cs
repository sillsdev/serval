using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddTranslationRepositories(
        this IMongoDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<Engine>(
            "translation.engines",
            mapSetup: ms =>
            {
                if (!BsonClassMap.IsClassMapRegistered(typeof(Corpus)))
                {
                    BsonClassMap.RegisterClassMap<Corpus>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
                    });
                }
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
                // migrate to new ParallelCorpora scheme by adding ParallelCorpora to existing engines
                await c.UpdateManyAsync(
                    Builders<Engine>.Filter.Exists(e => e.ParallelCorpora, false),
                    Builders<Engine>.Update.Set(e => e.ParallelCorpora, new List<ParallelCorpus>())
                );
            }
        );
        configurator.AddRepository<Build>(
            "translation.builds",
            mapSetup: ms =>
            {
                ms.MapMember(m => m.EngineRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                if (!BsonClassMap.IsClassMapRegistered(typeof(TrainingCorpus)))
                {
                    BsonClassMap.RegisterClassMap<TrainingCorpus>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(m => m.CorpusRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                        cm.MapMember(m => m.ParallelCorpusRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                    });
                }
                if (!BsonClassMap.IsClassMapRegistered(typeof(PretranslateCorpus)))
                {
                    BsonClassMap.RegisterClassMap<PretranslateCorpus>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(m => m.CorpusRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
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
                if (!BsonClassMap.IsClassMapRegistered(typeof(ParallelCorpusAnalysis)))
                {
                    BsonClassMap.RegisterClassMap<ParallelCorpusAnalysis>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(c => c.ParallelCorpusRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
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
                // migrate by adding ExecutionData field
                await c.UpdateManyAsync(
                    Builders<Build>.Filter.Exists(b => b.ExecutionData, false),
                    Builders<Build>.Update.Set(b => b.ExecutionData, new Dictionary<string, string>())
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
        configurator.AddRepository<Pretranslation>(
            "translation.pretranslations",
            mapSetup: ms =>
            {
                ms.MapMember(m => m.CorpusRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                ms.MapMember(m => m.EngineRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
            },
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Pretranslation>(
                        Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.ModelRevision)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Pretranslation>(
                        Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.CorpusRef)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Pretranslation>(Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.TextId))
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Pretranslation>(
                        Builders<Pretranslation>
                            .IndexKeys.Ascending(pt => pt.EngineRef)
                            .Ascending(pt => pt.ModelRevision)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Pretranslation>(
                        Builders<Pretranslation>
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
