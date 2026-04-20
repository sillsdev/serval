namespace Microsoft.Extensions.DependencyInjection;

public static class IServalConfiguratorExtensions
{
    public static IServalConfigurator AddTranslation(this IServalConfigurator configurator)
    {
        configurator.Services.AddScoped<IBuildService, BuildService>();
        configurator.Services.AddScoped<ContractMapper>();
        configurator.Services.AddScoped<IUsfmGenerationService, UsfmGenerationService>();
        configurator.Services.AddScoped<IEngineServiceFactory, EngineServiceFactory>();
        configurator.Services.AddScoped<DtoMapper>();
        configurator.Services.AddScoped<ITranslationPlatformService, PlatformService>();

        configurator.AddTranslationDataAccess();

        configurator.AddHandlers(Assembly.GetExecutingAssembly());

        return configurator;
    }

    public static IServalConfigurator AddTranslationDataAccess(this IServalConfigurator configurator)
    {
        configurator.DataAccess.AddRepository<Engine>(
            "translation.engines",
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
                // migrate to new ParallelCorpora scheme by adding ParallelCorpora to existing engines
                c =>
                    c.UpdateManyAsync(
                        Builders<Engine>.Filter.Exists(e => e.ParallelCorpora, false),
                        Builders<Engine>.Update.Set(e => e.ParallelCorpora, new List<ParallelCorpus>())
                    ),
            ]
        );
        configurator.DataAccess.AddRepository<Build>(
            "translation.builds",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.Owner))
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.EngineRef))
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.DateCreated))
                    ),
                // migrate by adding ExecutionData field
                c =>
                    c.UpdateManyAsync(
                        Builders<Build>.Filter.Exists(b => b.ExecutionData, false),
                        Builders<Build>.Update.Set(b => b.ExecutionData, new ExecutionData())
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
                // migrate by duplicating the owner field from build
                c =>
                    c.Aggregate()
                        .Match(Builders<Build>.Filter.Exists(b => b.Owner, false))
                        .Lookup("translation.engines", "engineRef", "_id", "engine")
                        .Unwind(
                            "engine",
                            new AggregateUnwindOptions<BsonDocument> { PreserveNullAndEmptyArrays = true }
                        )
                        .AppendStage<BsonDocument>(new BsonDocument("$set", new BsonDocument("owner", "$engine.owner")))
                        .AppendStage<BsonDocument>(new BsonDocument("$unset", "engine"))
                        .Merge(c, new MergeStageOptions<Build> { WhenMatched = MergeStageWhenMatched.Replace })
                        .ToListAsync(),
                MongoMigrations.MigrateTargetQuoteConvention,
            ]
        );
        configurator.DataAccess.AddRepository<Pretranslation>(
            "translation.pretranslations",
            init:
            [
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Pretranslation>(
                            Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.ModelRevision)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Pretranslation>(
                            Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.CorpusRef)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Pretranslation>(
                            Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.TextId)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Pretranslation>(
                            Builders<Pretranslation>
                                .IndexKeys.Ascending(pt => pt.EngineRef)
                                .Ascending(pt => pt.ModelRevision)
                        )
                    ),
                c =>
                    c.Indexes.CreateOrUpdateAsync(
                        new CreateIndexModel<Pretranslation>(
                            Builders<Pretranslation>
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
