namespace Serval.Machine.Shared.Services;

public interface IPlatformService
{
    EngineGroup EngineGroup { get; }
    Task IncrementTrainSizeAsync(string engineId, int count = 1, CancellationToken cancellationToken = default);

    Task UpdateBuildStatusAsync(
        string buildId,
        ProgressStatus progressStatus,
        int? queueDepth = null,
        CancellationToken cancellationToken = default
    );
    Task UpdateBuildStatusAsync(string buildId, int step, CancellationToken cancellationToken = default);
    Task BuildStartedAsync(string buildId, CancellationToken cancellationToken = default);
    Task BuildCompletedAsync(
        string buildId,
        int trainSize,
        double confidence,
        CancellationToken cancellationToken = default
    );
    Task BuildCanceledAsync(string buildId, CancellationToken cancellationToken = default);
    Task BuildFaultedAsync(string buildId, string message, CancellationToken cancellationToken = default);
    Task BuildRestartingAsync(string buildId, CancellationToken cancellationToken = default);

    Task InsertInferencesAsync(
        string engineId,
        Stream pretranslationsStream,
        CancellationToken cancellationToken = default
    );

    Task UpdateBuildExecutionDataAsync(
        string engineId,
        string buildId,
        IReadOnlyDictionary<string, string> executionData,
        CancellationToken cancellationToken = default
    );
}
