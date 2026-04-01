namespace Serval.Translation.Contracts;

public interface ITranslationEngineService
{
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

    Task<IReadOnlyList<TranslationResultContract>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    );
    Task<WordGraphContract> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    );
    Task TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    );
    Task<ModelDownloadUrlContract> GetModelDownloadUrlAsync(
        string engineId,
        CancellationToken cancellationToken = default
    );

    Task StartBuildAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> corpora,
        string? options = null,
        CancellationToken cancellationToken = default
    );

    Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default);

    Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default);
    Task<LanguageInfoContract> GetLanguageInfoAsync(string language, CancellationToken cancellationToken = default);
}
