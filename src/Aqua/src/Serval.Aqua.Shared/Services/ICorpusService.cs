namespace Serval.Aqua.Shared.Services;

public interface ICorpusService
{
    Task<Corpus?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task AddEngineAsync(CorpusData corpusData, string engineId, CancellationToken cancellationToken = default);
    Task<int> CreateRevisionAsync(CorpusData corpusData, CancellationToken cancellationToken = default);
    Task RemoveEngineAsync(string engineId, CancellationToken cancellationToken = default);
}
