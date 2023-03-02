using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Serval.AspNetCore;

[TestFixture]
public class Test
{
    [Test]
    public async Task TestSubscribe()
    {
        DataAccessClassMap.RegisterConventions(
            "Serval.AspNetCore.Models",
            new StringIdStoredAsObjectIdConvention(),
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreIfNullConvention(true),
            new ObjectRefConvention()
        );

        var mongoUrl = new MongoUrl("mongodb://localhost:27017/serval");

        var mongoClient = new MongoClient(mongoUrl);
        var database = mongoClient.GetDatabase(mongoUrl.DatabaseName);
        var context = new MongoDataAccessContext(mongoClient);

        var builds = database.GetCollection<Build>("builds");
        builds.Indexes.CreateOrUpdate(
            new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.EngineRef))
        );

        var repo = new MongoRepository<Build>(context, builds);

        using var sub = await repo.SubscribeAsync(b => b.EngineRef == "61a9c1f3b0c4e2d7f8a6b5d7");
        while (true)
        {
            await sub.WaitForChangeAsync();
            if (sub.Change.Type == EntityChangeType.Delete)
                break;
        }
    }
}
