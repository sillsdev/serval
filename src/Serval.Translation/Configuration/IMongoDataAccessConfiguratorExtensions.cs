using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddTranslationRepositories(
        this IMongoDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<TranslationEngine>(
            "translation.engines",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<TranslationEngine>(
                        Builders<TranslationEngine>.IndexKeys.Ascending(e => e.Owner)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<TranslationEngine>(
                        Builders<TranslationEngine>.IndexKeys.Ascending("corpora._id")
                    )
                );
            }
        );
        configurator.AddRepository<Build>(
            "translation.builds",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.EngineRef))
                )
        );
        configurator.AddRepository<Pretranslation>(
            "translation.pretranslations",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Pretranslation>(
                        Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.EngineRef)
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
            }
        );
        return configurator;
    }
}
