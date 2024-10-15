namespace Serval.Machine.Shared.Services;

public interface IWordAlignmentEngineService
{
    WordAlignmentEngineType WordAlignmentEngine { get; }

    Task<WordAlignmentEngine> CreateAsync(
        string engineId,
        string? engineName,
        string sourceLanguage,
        string targetLanguage,
        bool? isModelPersisted = null,
        CancellationToken cancellationToken = default
    );
    Task DeleteAsync(string engineId, CancellationToken cancellationToken = default);

    Task<TranslationResult> GetBestPhraseAlignmentAsync(
        string sourceSegment,
        string targetSegment,
        CancellationToken cancellationToken = default
    );

    Task StartBuildAsync(
        string engineId,
        string buildId,
        string? buildOptions,
        IReadOnlyList<ParallelCorpus> corpora,
        CancellationToken cancellationToken = default
    );

    Task CancelBuildAsync(string engineId, CancellationToken cancellationToken = default);

    int GetQueueSize();
}
