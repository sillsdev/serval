namespace Serval.Machine.Shared.Services;

public interface IClearMLBuildJobFactory
{
    EngineType EngineType { get; }

    Task<string> CreateJobScriptAsync(
        string engineId,
        string buildId,
        string modelType,
        BuildStage stage,
        object? data = null,
        string? buildOptions = null,
        CancellationToken cancellationToken = default
    );
}
