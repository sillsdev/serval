namespace Microsoft.Extensions.DependencyInjection;

public static class IMemoryDataAccessConfiguratorExtensions
{
    public static IMemoryDataAccessConfigurator AddAssessmentRepositories(
        this IMemoryDataAccessConfigurator configurator
    )
    {
        configurator.AddRepository<Engine>();
        configurator.AddRepository<Job>();
        configurator.AddRepository<Result>();
        return configurator;
    }
}
