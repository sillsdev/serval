namespace Serval.Machine.Shared.Services;

public interface IPlatformService
{
    Task IncrementTrainSizeAsync(string engineId, int count = 1, CancellationToken cancellationToken = default);

    Task UpdateJobStatusAsync(
        string buildId,
        ProgressStatus progressStatus,
        int? queueDepth = null,
        CancellationToken cancellationToken = default
    );
    Task UpdateJobStatusAsync(string buildId, int step, CancellationToken cancellationToken = default);
    Task JobStartedAsync(string buildId, CancellationToken cancellationToken = default);
    Task JobCompletedAsync(
        string buildId,
        int trainSize,
        double confidence,
        CancellationToken cancellationToken = default
    );
    Task JobCanceledAsync(string buildId, CancellationToken cancellationToken = default);
    Task JobFaultedAsync(string buildId, string message, CancellationToken cancellationToken = default);
    Task JobRestartingAsync(string buildId, CancellationToken cancellationToken = default);

    Task InsertPretranslationsAsync(
        string engineId,
        Stream pretranslationsStream,
        CancellationToken cancellationToken = default
    );
}
