namespace Serval.WordAlignment.Configuration;

public static class MongoMigrations
{
    public static async Task MigrateRefsToTargetRefs(IMongoCollection<Models.WordAlignment> c)
    {
        await c.Aggregate()
            .Match(Builders<Models.WordAlignment>.Filter.Exists("refs"))
            .AppendStage<BsonDocument>(
                new BsonDocument(
                    "$set",
                    new BsonDocument(
                        "targetRefs",
                        new BsonDocument("$ifNull", new BsonArray { "$targetRefs", "$refs" })
                    )
                )
            )
            .AppendStage<BsonDocument>(new BsonDocument("$unset", "refs"))
            .Merge(c, new MergeStageOptions<Models.WordAlignment> { WhenMatched = MergeStageWhenMatched.Replace })
            .ToListAsync();
    }
}
