namespace Serval.Translation.Services;

public interface IEngineService
{
    Task<IEnumerable<Engine>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<Engine?> GetAsync(string engineId, CancellationToken cancellationToken = default);

    Task<bool> CreateAsync(Engine engine, CancellationToken cancellationToken = default);
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

    Task<bool> StartBuildAsync(Build build, CancellationToken cancellationToken = default);

    Task CancelBuildAsync(string engineId, CancellationToken cancellationToken = default);

    Task AddCorpusAsync(string engineId, Corpus corpus, CancellationToken cancellationToken = default);
    Task<Corpus?> UpdateCorpusAsync(
        string engineId,
        string corpusId,
        IList<CorpusFile>? sourceFiles,
        IList<CorpusFile>? targetFiles,
        CancellationToken cancellationToken = default
    );
    Task<bool> DeleteCorpusAsync(string engineId, string corpusId, CancellationToken cancellationToken = default);

    Task DeleteAllCorpusFilesAsync(string dataFileId, CancellationToken cancellationToken = default);

    Task<Queue> GetQueueAsync(string engineType, string? queue = null, CancellationToken cancellationToken = default);
}
