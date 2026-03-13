namespace Serval.Translation.Services;

public interface IEngineService
{
    Task<Engine> GetAsync(string engineId, CancellationToken cancellationToken = default);

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
}
