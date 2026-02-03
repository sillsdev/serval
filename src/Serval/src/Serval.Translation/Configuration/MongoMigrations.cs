using MongoDB.Bson;
using MongoDB.Driver;

namespace Serval.Translation.Configuration;

public class MongoMigrations
{
    public static async Task MigrateTargetQuoteConvention(IMongoCollection<Build> c)
    {
        // migrate by adding TargetQuoteConvention field populated from analysis field
        await c.Aggregate()
            .Match(Builders<Build>.Filter.Exists(b => b.TargetQuoteConvention, false))
            .Match(Builders<Build>.Filter.Exists("analysis"))
            .AppendStage<BsonDocument>(
                new BsonDocument(
                    "$set",
                    new BsonDocument(
                        "targetQuoteConvention",
                        new BsonDocument(
                            "$ifNull",
                            new BsonArray()
                            {
                                new BsonDocument(
                                    "$first",
                                    new BsonDocument(
                                        "$map",
                                        new BsonDocument
                                        {
                                            {
                                                "input",
                                                new BsonDocument(
                                                    "$filter",
                                                    new BsonDocument
                                                    {
                                                        { "input", "$analysis" },
                                                        { "as", "a" },
                                                        {
                                                            "cond",
                                                            new BsonDocument(
                                                                "$ne",
                                                                new BsonArray { "$$a.targetQuoteConvention", "" }
                                                            )
                                                        }
                                                    }
                                                )
                                            },
                                            { "as", "a" },
                                            { "in", "$$a.targetQuoteConvention" }
                                        }
                                    )
                                ),
                                ""
                            }
                        )
                    )
                )
            )
            .Merge(c, new MergeStageOptions<Build> { WhenMatched = MergeStageWhenMatched.Replace })
            .ToListAsync();
    }
}
