namespace Serval.Machine.Shared.Services;

public class StatisticalTrainBuildJob(
    IEnumerable<IPlatformService> platformServices,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ILogger<StatisticalTrainBuildJob> logger
)
    : HangfireBuildJob<WordAlignmentEngine>(
        platformServices.First(ps => ps.EngineGroup == EngineGroup.WordAlignment),
        engines,
        dataAccessContext,
        buildJobService,
        logger
    )
{
    protected override Task DoWorkAsync(
        string engineId,
        string buildId,
        object? data,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        throw new NotImplementedException();
    }
}
