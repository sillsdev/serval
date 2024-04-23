namespace Serval.Assessment.Services;

public interface ICorpusService
{
    Task<IEnumerable<Corpus>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<Corpus> GetAsync(string corpusId, CancellationToken cancellationToken = default);

    Task<Corpus> CreateAsync(Corpus corpus, CancellationToken cancellationToken = default);
    Task<Corpus> UpdateAsync(
        string corpusId,
        IReadOnlyList<CorpusFile> files,
        CancellationToken cancellationToken = default
    );
    Task DeleteAsync(string corpusId, CancellationToken cancellationToken = default);

    Task DataFileUpdated(string dataFileId, CancellationToken cancellationToken = default);
    Task DataFileDeleted(string dataFileId, CancellationToken cancellationToken = default);
}
