using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using CorpusFile = Serval.DataFiles.Models.CorpusFile;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddDataFilesRepositories(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<DataFile>(
            "data_files.files",
            mapSetup: ms => ms.MapIdMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId)),
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<DataFile>(Builders<DataFile>.IndexKeys.Ascending(p => p.Owner))
                )
        );

        configurator.AddRepository<DeletedFile>(
            "data_files.deleted_files",
            mapSetup: ms => ms.MapIdMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId)),
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<DeletedFile>(Builders<DeletedFile>.IndexKeys.Ascending(p => p.DeletedAt))
                )
        );
        configurator.AddRepository<Corpus>(
            "corpora.corpus",
            mapSetup: ms =>
            {
                ms.MapIdMember(m => m.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
                if (!BsonClassMap.IsClassMapRegistered(typeof(CorpusFile)))
                {
                    BsonClassMap.RegisterClassMap<CorpusFile>(cm =>
                    {
                        cm.AutoMap();
                        cm.MapMember(c => c.FileRef).SetSerializer(new StringSerializer(BsonType.ObjectId));
                    });
                }
            },
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
        return configurator;
    }
}
