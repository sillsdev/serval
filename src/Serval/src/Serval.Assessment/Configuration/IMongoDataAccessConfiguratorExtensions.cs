using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

public static class IMongoDataAccessConfiguratorExtensions
{
    public static IMongoDataAccessConfigurator AddAssessmentRepositories(this IMongoDataAccessConfigurator configurator)
    {
        configurator.AddRepository<AssessmentEngine>(
            "assessment.engines",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<AssessmentEngine>(Builders<AssessmentEngine>.IndexKeys.Ascending(e => e.Owner))
                );
            }
        );
        configurator.AddRepository<AssessmentJob>(
            "assessment.jobs",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<AssessmentJob>(Builders<AssessmentJob>.IndexKeys.Ascending(b => b.EngineRef))
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
