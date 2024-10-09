namespace Serval.DataFiles.Services;

public class CorpusService(IRepository<Corpus> corpora) : OwnedEntityServiceBase<Corpus>(corpora), ICorpusService
{
    public async Task<Corpus> GetAsync(string id, string owner, CancellationToken cancellationToken = default)
    {
        Corpus? corpus = await Entities.GetAsync(c => c.Id == id && c.Owner == owner, cancellationToken);
        if (corpus == null)
            throw new EntityNotFoundException($"Could not find the Corpus '{id}' with owner '{owner}'.");
        return corpus;
    }

    public async Task<Corpus> UpdateAsync(
        string id,
        IReadOnlyList<CorpusFile> files,
        CancellationToken cancellationToken = default
    )
    {
        Corpus? corpus = await Entities.UpdateAsync(
            c => c.Id == id,
            u => u.Set(c => c.Files, files),
            cancellationToken: cancellationToken
        );
        if (corpus is null)
            throw new EntityNotFoundException($"Could not find Corpus '{id}.");
        return corpus;
    }
}
