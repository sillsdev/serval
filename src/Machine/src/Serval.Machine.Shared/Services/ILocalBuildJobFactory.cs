namespace Serval.Machine.Shared.Services;

public interface ILocalBuildJobFactory
{
    EngineType EngineType { get; }

    string? Serialize(BuildStage stage, object? data);

    Task RunAsync(
        IServiceProvider serviceProvider,
        string engineId,
        string buildId,
        BuildStage stage,
        string? jobData,
        string? buildOptions,
        CancellationToken cancellationToken
    );
}
