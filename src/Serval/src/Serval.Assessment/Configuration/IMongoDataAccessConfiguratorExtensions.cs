using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddAssessmentRepositories(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<Corpus>(
            "assessment.corpora",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Corpus>(Builders<Corpus>.IndexKeys.Ascending(c => c.Owner))
                );
            }
        );

        configurator.AddRepository<Engine>(
            "assessment.engines",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Engine>(Builders<Engine>.IndexKeys.Ascending(e => e.Owner))
                );
            }
        );
        configurator.AddRepository<Job>(
            "assessment.jobs",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Job>(Builders<Job>.IndexKeys.Ascending(b => b.EngineRef))
                )
        );
        configurator.AddRepository<Result>(
            "assessment.results",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Result>(Builders<Result>.IndexKeys.Ascending(pt => pt.EngineRef))
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Result>(Builders<Result>.IndexKeys.Ascending(pt => pt.JobRef))
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<Result>(Builders<Result>.IndexKeys.Ascending(pt => pt.TextId))
                );
            }
        );
        return configurator;
    }
}
