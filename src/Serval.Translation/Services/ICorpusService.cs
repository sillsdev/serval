namespace Serval.Translation.Services;

public interface ICorpusService
{
    Task<Corpus?> GetAsync(string id, CancellationToken cancellationToken = default);
}
