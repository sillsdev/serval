namespace Serval.WordAlignment.Contracts;

public interface IWordAlignmentPlatformService
{
    Task BuildStartedAsync(string buildId, CancellationToken cancellationToken = default);
    Task BuildCompletedAsync(
        string buildId,
        int corpusSize,
        double confidence,
        CancellationToken cancellationToken = default
    );
    Task BuildCanceledAsync(string buildId, CancellationToken cancellationToken = default);
    Task BuildFaultedAsync(string buildId, string message, CancellationToken cancellationToken = default);
    Task BuildRestartingAsync(string buildId, CancellationToken cancellationToken = default);
    Task UpdateBuildStatusAsync(
        string buildId,
        BuildProgressStatusContract progressStatus,
        int? queueDepth = null,
        IReadOnlyCollection<BuildPhaseContract>? phases = null,
        DateTime? started = null,
        DateTime? completed = null,
        CancellationToken cancellationToken = default
    );
    Task UpdateBuildStatusAsync(string buildId, int step, CancellationToken cancellationToken = default);
    Task IncrementEngineCorpusSizeAsync(string engineId, int count = 1, CancellationToken cancellationToken = default);
    Task InsertWordAlignmentsAsync(
        string engineId,
        IAsyncEnumerable<WordAlignmentContract> wordAlignments,
        CancellationToken cancellationToken = default
    );
    Task UpdateBuildExecutionDataAsync(
        string engineId,
        string buildId,
        ExecutionDataContract executionData,
        CancellationToken cancellationToken = default
    );
}
