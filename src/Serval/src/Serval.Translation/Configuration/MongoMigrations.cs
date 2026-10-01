namespace Serval.Translation.Configuration;

public static class MongoMigrations
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
                                                        },
                                                    }
                                                )
                                            },
                                            { "as", "a" },
                                            { "in", "$$a.targetQuoteConvention" },
                                        }
                                    )
                                ),
                                "",
                            }
                        )
                    )
                )
            )
            .Merge(c, new MergeStageOptions<Build> { WhenMatched = MergeStageWhenMatched.Replace })
            .ToListAsync();
    }

    public static async Task MigrateRefsToTargetRefs(IMongoCollection<Pretranslation> c)
    {
        await c.Aggregate()
            .Match(Builders<Pretranslation>.Filter.Exists("refs"))
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
            .Merge(c, new MergeStageOptions<Pretranslation> { WhenMatched = MergeStageWhenMatched.Replace })
            .ToListAsync();
    }
}
