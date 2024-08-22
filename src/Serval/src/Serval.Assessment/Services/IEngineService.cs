namespace Serval.Assessment.Services;

public interface IEngineService
{
    Task<IEnumerable<Engine>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<Engine> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<Engine> CreateAsync(Engine engine, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<Corpus> ReplaceCorpusAsync(string id, Corpus corpus, CancellationToken cancellationToken = default);
    Task<Corpus> ReplaceReferenceCorpusAsync(
        string id,
        Corpus referenceCorpus,
        CancellationToken cancellationToken = default
    );

    Task StartJobAsync(Job job, CancellationToken cancellationToken = default);
    Task<bool> CancelJobAsync(string id, string jobId, CancellationToken cancellationToken = default);

    Task DeleteAllCorpusFilesAsync(string dataFileId, CancellationToken cancellationToken = default);
}
