namespace Serval.Translation.Services;

public interface IEngineService
{
    Task<IEnumerable<Engine>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<Engine> GetAsync(string engineId, CancellationToken cancellationToken = default);

    Task<Engine> CreateAsync(Engine engine, CancellationToken cancellationToken = default);

    Task UpdateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default
    );

    Task DeleteAsync(string engineId, CancellationToken cancellationToken = default);

    Task<TranslationResult> TranslateAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    );

    Task<IEnumerable<TranslationResult>> TranslateAsync(
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

    Task StartBuildAsync(Build build, CancellationToken cancellationToken = default);

    Task<bool> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default);

    Task<ModelDownloadUrl> GetModelDownloadUrlAsync(string engineId, CancellationToken cancellationToken = default);

    Task AddCorpusAsync(string engineId, Corpus corpus, CancellationToken cancellationToken = default);
    Task<Corpus> UpdateCorpusAsync(
        string engineId,
        string corpusId,
        IReadOnlyList<CorpusFile>? sourceFiles,
        IReadOnlyList<CorpusFile>? targetFiles,
        CancellationToken cancellationToken = default
    );
    Task DeleteCorpusAsync(
        string engineId,
        string corpusId,
        bool deleteFiles,
        CancellationToken cancellationToken = default
    );

    Task AddParallelCorpusAsync(string engineId, ParallelCorpus corpus, CancellationToken cancellationToken = default);
    Task<ParallelCorpus> UpdateParallelCorpusAsync(
        string engineId,
        string parallelCorpusId,
        IReadOnlyList<MonolingualCorpus>? sourceCorpora,
        IReadOnlyList<MonolingualCorpus>? targetCorpora,
        CancellationToken cancellationToken = default
    );
    Task DeleteParallelCorpusAsync(
        string engineId,
        string parallelCorpusId,
        CancellationToken cancellationToken = default
    );

    Task DeleteAllCorpusFilesAsync(string dataFileId, CancellationToken cancellationToken = default);

    Task UpdateDataFileFilenameFilesAsync(
        string dataFileId,
        string filename,
        CancellationToken cancellationToken = default
    );

    Task UpdateCorpusFilesAsync(
        string corpusId,
        IReadOnlyList<CorpusFile> files,
        CancellationToken cancellationToken = default
    );

    Task<Queue> GetQueueAsync(string engineType, CancellationToken cancellationToken = default);

    Task<LanguageInfo> GetLanguageInfoAsync(
        string engineType,
        string language,
        CancellationToken cancellationToken = default
    );
}
