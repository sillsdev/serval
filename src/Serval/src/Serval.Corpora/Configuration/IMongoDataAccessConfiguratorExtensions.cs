using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddCorporaRepository(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<Corpus>(
            "corpora.corpus",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Corpus>(Builders<Corpus>.IndexKeys.Ascending(p => p.Owner))
                )
        );
        return configurator;
    }
}
