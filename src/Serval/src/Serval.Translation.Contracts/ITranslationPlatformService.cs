namespace Serval.Translation.Contracts;

public interface ITranslationPlatformService
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
        BuildProgressStatus progressStatus,
        int? queueDepth = null,
        IReadOnlyCollection<BuildPhase>? phases = null,
        DateTime? started = null,
        DateTime? completed = null,
        CancellationToken cancellationToken = default
    );
    Task UpdateBuildStatusAsync(string buildId, int step, CancellationToken cancellationToken = default);
    Task IncrementEngineCorpusSizeAsync(string engineId, int count = 1, CancellationToken cancellationToken = default);
    Task InsertPretranslationsAsync(
        string engineId,
        IAsyncEnumerable<PretranslationData> pretranslations,
        CancellationToken cancellationToken = default
    );
    Task UpdateBuildExecutionDataAsync(
        string engineId,
        string buildId,
        ExecutionData executionData,
        CancellationToken cancellationToken = default
    );
    Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        string quoteConvention,
        CancellationToken cancellationToken = default
    );
}
