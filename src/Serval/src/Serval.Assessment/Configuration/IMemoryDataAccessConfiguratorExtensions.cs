namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddAssessmentRepositories(
        this IMemoryDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<AssessmentEngine>();
        configurator.AddRepository<AssessmentJob>();
        configurator.AddRepository<Result>();
        return configurator;
    }
}
