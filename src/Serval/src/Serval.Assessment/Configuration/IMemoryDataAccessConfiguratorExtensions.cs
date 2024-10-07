namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddAssessmentRepositories(
        this IMemoryDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<AssessmentEngine>();
        configurator.AddRepository<AssessmentBuild>();
        configurator.AddRepository<AssessmentResult>();
        return configurator;
    }
}
