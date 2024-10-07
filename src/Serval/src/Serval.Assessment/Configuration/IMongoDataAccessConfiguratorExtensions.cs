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
        configurator.AddRepository<AssessmentBuild>(
            "assessment.jobs",
            init: c =>
                c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<AssessmentBuild>(
                        Builders<AssessmentBuild>.IndexKeys.Ascending(b => b.EngineRef)
                    )
                )
        );
        configurator.AddRepository<AssessmentResult>(
            "assessment.results",
            init: async c =>
            {
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<AssessmentResult>(
                        Builders<AssessmentResult>.IndexKeys.Ascending(pt => pt.EngineRef)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<AssessmentResult>(
                        Builders<AssessmentResult>.IndexKeys.Ascending(pt => pt.BuildRevision)
                    )
                );
                await c.Indexes.CreateOrUpdateAsync(
                    new CreateIndexModel<AssessmentResult>(
                        Builders<AssessmentResult>.IndexKeys.Ascending(pt => pt.TextId)
                    )
                );
            }
        );
        return configurator;
    }
}
