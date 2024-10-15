using Microsoft.Extensions.DependencyInjection;
using SIL.ServiceToolkit.Services;

namespace Serval.Translation.Services;

public class BuildCleanupService(
    IServiceProvider services,
    ILogger<BuildCleanupService> logger,
    TimeSpan? timeout = null
) : RecurrentTask("Build Cleanup Service", services, RefreshPeriod, logger)
{
    private readonly ILogger<BuildCleanupService> _logger = logger;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RefreshPeriod = TimeSpan.FromDays(1);

    protected override async Task DoWorkAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running build cleanup job");
        var builds = scope.ServiceProvider.GetRequiredService<IRepository<Build>>();
        await CheckBuildsAsync(builds, cancellationToken);
    }

    public async Task CheckBuildsAsync(IRepository<Build> builds, CancellationToken cancellationToken)
    {
        IReadOnlyList<Build> allBuilds = await builds.GetAllAsync(cancellationToken);
        IEnumerable<Build> notStartedBuilds = allBuilds.Where(b => !b.SuccessfullyStarted);
        await Task.Delay(_timeout, cancellationToken); //Make sure the builds are not midway through starting
        foreach (
            Build build in await builds.GetAllAsync(
                b => notStartedBuilds.Select(c => c.Id).Contains(b.Id),
                cancellationToken
            )
        )
        {
            if (!build.SuccessfullyStarted)
            {
                _logger.LogInformation("Deleting build {id} because it was never successfully started", build.Id);
                await builds.DeleteAsync(build, cancellationToken);
            }
        }
    }
}
