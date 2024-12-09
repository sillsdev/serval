namespace Serval.WordAlignment.Services;

public interface IEngineService
{
    Task<IEnumerable<Engine>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<Engine> GetAsync(string engineId, CancellationToken cancellationToken = default);

    Task<Engine> CreateAsync(Engine engine, CancellationToken cancellationToken = default);
    Task DeleteAsync(string engineId, CancellationToken cancellationToken = default);

    Task<WordAlignmentResult> GetWordAlignmentAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        CancellationToken cancellationToken = default
    );

    Task StartBuildAsync(Build build, CancellationToken cancellationToken = default);

    Task<bool> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default);

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
}
