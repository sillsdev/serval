namespace Serval.Machine.Shared.Services;

public interface IBuildJobService<TEngine> : IBuildJobServiceBase
    where TEngine : ITrainingEngine
{
    Task<IReadOnlyList<TEngine>> GetBuildingEnginesAsync(
        BuildJobRunnerType runner,
        CancellationToken cancellationToken = default
    );
}
