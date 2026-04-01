namespace Serval.DataFiles.Services;

public class CorpusService(
    IRepository<Corpus> corpora,
    IRepository<DataFile> dataFiles,
    IDataAccessContext dataAccessContext,
    IEventRouter eventRouter
) : OwnedEntityServiceBase<Corpus>(corpora), ICorpusService
{
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<DataFile> _dataFiles = dataFiles;
    private readonly IEventRouter _eventRouter = eventRouter;

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
        return await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Corpus? corpus = await Entities.UpdateAsync(
                    c => c.Id == id,
                    u => u.Set(c => c.Files, files),
                    cancellationToken: cancellationToken
                );
                if (corpus is null)
                    throw new EntityNotFoundException($"Could not find Corpus '{id}.");
                HashSet<string> corpusFileIds = corpus.Files.Select(f => f.FileRef).ToHashSet();
                IDictionary<string, DataFile> corpusDataFilesDict = (
                    await _dataFiles.GetAllAsync(f => corpusFileIds.Contains(f.Id), ct)
                ).ToDictionary(f => f.Id);
                await _eventRouter.PublishAsync(
                    new CorpusUpdated(
                        corpus.Id,
                        [
                            .. corpus.Files.Select(f => new CorpusDataFileContract(
                                File: Map(corpusDataFilesDict[f.FileRef]),
                                f.TextId ?? corpusDataFilesDict[f.FileRef].Name
                            )),
                        ]
                    ),
                    ct
                );
                return corpus;
            },
            cancellationToken
        );
    }

    public Task DeleteAllCorpusFilesAsync(string fileId, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAllAsync(
            e => e.Files.Any(f => f.FileRef == fileId),
            u =>
            {
                u.RemoveAll(e => e.Files, f => f.FileRef == fileId);
            },
            cancellationToken: cancellationToken
        );
    }

    private static DataFileContract Map(DataFile dataFile)
    {
        return new DataFileContract(dataFile.Id, dataFile.Name, dataFile.Filename, dataFile.Format);
    }
}
