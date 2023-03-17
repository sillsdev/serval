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
            init: c =>
                c.Indexes.CreateOrUpdate(
                    new CreateIndexModel<TranslationEngine>(
                        Builders<TranslationEngine>.IndexKeys.Ascending(p => p.Owner)
                    )
                )
        );
        configurator.AddRepository<Build>(
            "translation.builds",
            init: c =>
                c.Indexes.CreateOrUpdate(
                    new CreateIndexModel<Build>(Builders<Build>.IndexKeys.Ascending(b => b.EngineRef))
                )
        );
        configurator.AddRepository<Pretranslation>(
            "translation.pretranslations",
            init: c =>
            {
                c.Indexes.CreateOrUpdate(
                    new CreateIndexModel<Pretranslation>(
                        Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.EngineRef)
                    )
                );
                c.Indexes.CreateOrUpdate(
                    new CreateIndexModel<Pretranslation>(
                        Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.CorpusRef)
                    )
                );
                c.Indexes.CreateOrUpdate(
                    new CreateIndexModel<Pretranslation>(Builders<Pretranslation>.IndexKeys.Ascending(pt => pt.TextId))
                );
            }
        );
        return configurator;
    }
}
