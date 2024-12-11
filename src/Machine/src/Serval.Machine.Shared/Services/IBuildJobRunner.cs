namespace Serval.Machine.Shared.Services;

public interface IBuildJobRunner
{
    BuildJobRunnerType Type { get; }

    Task CreateEngineAsync(string engineId, string? name = null, CancellationToken cancellationToken = default);
    Task DeleteEngineAsync(string engineId, CancellationToken cancellationToken = default);

    Task<string> CreateJobAsync(
        EngineType engineType,
        string engineId,
        string buildId,
        BuildStage stage,
        object? data = null,
        string? buildOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteJobAsync(string jobId, CancellationToken cancellationToken = default);

    Task<bool> EnqueueJobAsync(string jobId, EngineType engineType, CancellationToken cancellationToken = default);

    Task<bool> StopJobAsync(string jobId, CancellationToken cancellationToken = default);
}
