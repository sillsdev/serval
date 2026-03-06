namespace Serval.Translation.Services;

public interface ITranslationEngineService
{
    string Type { get; }

    // Lifecycle
    Task CreateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        string? engineName = null,
        bool? isModelPersisted = null,
        CancellationToken cancellationToken = default
    );
    Task DeleteAsync(string engineId, CancellationToken cancellationToken = default);
    Task UpdateAsync(
        string engineId,
        string? sourceLanguage,
        string? targetLanguage,
        CancellationToken cancellationToken = default
    );

    // Inference
    Task<IReadOnlyList<TranslationResult>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    );
    Task<WordGraph> GetWordGraphAsync(string engineId, string segment, CancellationToken cancellationToken = default);
    Task TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    );
    Task<ModelDownloadUrl?> GetModelDownloadUrlAsync(string engineId, CancellationToken cancellationToken = default);

    // Build lifecycle
    Task StartBuildAsync(
        string engineId,
        string buildId,
        IReadOnlyList<FilteredParallelCorpus> corpora,
        string? options = null,
        CancellationToken cancellationToken = default
    );

    /// <returns>The cancelled build ID, or <c>null</c> if no build was active.</returns>
    Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default);

    // Per-engine-type queries
    Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default);
    Task<LanguageInfo> GetLanguageInfoAsync(string language, CancellationToken cancellationToken = default);
}
