namespace Serval.DataFiles.Services;

public class CorpusService(
    IRepository<Corpus> corpora,
    IRepository<DataFile> dataFiles,
    IDataAccessContext dataAccessContext,
    IScopedMediator mediator
) : OwnedEntityServiceBase<Corpus>(corpora), ICorpusService
{
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<DataFile> _dataFiles = dataFiles;
    private readonly IScopedMediator _mediator = mediator;

    public async Task<Corpus> GetAsync(string id, string owner, CancellationToken cancellationToken = default)
    {
        Corpus? corpus = await Entities.GetAsync(c => c.Id == id && c.Owner == owner, cancellationToken);
        if (corpus == null)
            throw new EntityNotFoundException($"Could not find the Corpus '{id}' with owner '{owner}'.");
        return corpus;
    }

    public async Task<Corpus> UpdateAsync(
        string id,
        IReadOnlyList<Models.CorpusFile> files,
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
                await _mediator.Publish(
                    new CorpusUpdated
                    {
                        CorpusId = corpus.Id,
                        Files = corpus
                            .Files.Select(f => new CorpusFileResult
                            {
                                TextId = f.TextId ?? corpusDataFilesDict[f.FileRef].Name,
                                File = Map(corpusDataFilesDict[f.FileRef])
                            })
                            .ToList()
                    },
                    ct
                );
                return corpus;
            },
            cancellationToken
        );
    }

    private static DataFileResult Map(DataFile dataFile)
    {
        return new DataFileResult
        {
            DataFileId = dataFile.Id,
            Name = dataFile.Name,
            Filename = dataFile.Filename,
            Format = dataFile.Format,
        };
    }
}
