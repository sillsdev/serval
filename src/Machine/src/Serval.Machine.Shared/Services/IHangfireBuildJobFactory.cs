namespace Serval.Machine.Shared.Services;

public interface IHangfireBuildJobFactory
{
    EngineType EngineType { get; }

    Job CreateJob(string engineId, string buildId, BuildStage stage, object? data, string? buildOptions);
}
