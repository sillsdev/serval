namespace Serval.Machine.Shared.Services;

public interface IBuildJobService
{
    Task<bool> IsEngineBuilding(string engineId, CancellationToken cancellationToken = default);

    Task CreateEngineAsync(string engineId, string? name = null, CancellationToken cancellationToken = default);

    Task DeleteEngineAsync(string engineId, CancellationToken cancellationToken = default);

    Task<bool> StartBuildJobAsync(
        BuildJobRunnerType runnerType,
        EngineType engineType,
        string engineId,
        string buildId,
        BuildStage stage,
        object? data = default,
        string? buildOptions = default,
        CancellationToken cancellationToken = default
    );

    Task<(string? BuildId, BuildJobState State)> CancelBuildJobAsync(
        string engineId,
        CancellationToken cancellationToken = default
    );

    Task<bool> BuildJobStartedAsync(string engineId, string buildId, CancellationToken cancellationToken = default);

    Task BuildJobFinishedAsync(
        string engineId,
        string buildId,
        bool buildComplete,
        CancellationToken cancellationToken = default
    );

    Task BuildJobRestartingAsync(string engineId, string buildId, CancellationToken cancellationToken = default);
}

public interface IBuildJobService<TEngine> : IBuildJobService
    where TEngine : ITrainingEngine
{
    Task<IReadOnlyList<TEngine>> GetBuildingEnginesAsync(
        BuildJobRunnerType runner,
        CancellationToken cancellationToken = default
    );
}
