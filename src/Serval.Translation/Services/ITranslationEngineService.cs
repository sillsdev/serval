namespace Serval.Translation.Services;

public interface ITranslationEngineService
{
    Task<IEnumerable<TranslationEngine>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<TranslationEngine?> GetAsync(string engineId, CancellationToken cancellationToken = default);

    Task CreateAsync(TranslationEngine engine, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string engineId, CancellationToken cancellationToken = default);

    Task<TranslationResult?> TranslateAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    );

    Task<IEnumerable<TranslationResult>?> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    );

    Task<WordGraph?> GetWordGraphAsync(string engineId, string segment, CancellationToken cancellationToken = default);

    Task<bool> TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    );

    Task<Build?> StartBuildAsync(string engineId, CancellationToken cancellationToken = default);

    Task CancelBuildAsync(string engineId, CancellationToken cancellationToken = default);

    Task AddCorpusAsync(string engineId, Corpus corpus, CancellationToken cancellationToken = default);
    Task<bool> DeleteCorpusAsync(string engineId, string corpusId, CancellationToken cancellationToken = default);

    Task DeleteAllCorpusFilesAsync(string dataFileId, CancellationToken cancellationToken = default);
}
