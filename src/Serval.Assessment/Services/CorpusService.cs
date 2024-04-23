namespace Serval.Assessment.Services;

public class CorpusService(IRepository<Corpus> corpora) : OwnedEntityServiceBase<Corpus>(corpora), ICorpusService
{
    public async Task<Corpus> UpdateAsync(
        string corpusId,
        IReadOnlyList<CorpusFile> files,
        CancellationToken cancellationToken = default
    )
    {
        Corpus? corpus = await Entities.UpdateAsync(
            e => e.Id == corpusId,
            u => u.Set(c => c.Files, files).Inc(c => c.DataRevision),
            cancellationToken: cancellationToken
        );
        if (corpus is null)
            throw new EntityNotFoundException($"Could not find the Corpus '{corpusId}'.");
        return corpus;
    }

    public Task DataFileUpdated(string dataFileId, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAllAsync(
            e => e.Files.Any(f => f.Id == dataFileId),
            u => u.Inc(c => c.DataRevision),
            cancellationToken
        );
    }

    public Task DataFileDeleted(string dataFileId, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAllAsync(
            e => e.Files.Any(f => f.Id == dataFileId),
            u => u.RemoveAll(c => c.Files, f => f.Id == dataFileId).Inc(c => c.DataRevision),
            cancellationToken
        );
    }
}
