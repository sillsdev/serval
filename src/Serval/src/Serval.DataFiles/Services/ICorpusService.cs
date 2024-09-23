namespace Serval.DataFiles.Services;

public interface ICorpusService
{
    Task<IEnumerable<Corpus>> GetAllAsync(string owner, CancellationToken cancellationToken);
    Task<Corpus> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<Corpus> GetAsync(string id, string owner, CancellationToken cancellationToken = default);
    Task<Corpus> CreateAsync(Corpus corpus, CancellationToken cancellationToken = default);
    Task<Corpus> UpdateAsync(string id, IReadOnlyList<CorpusFile> files, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
