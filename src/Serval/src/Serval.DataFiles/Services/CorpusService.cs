namespace Serval.DataFiles.Services;

public class CorpusService(
    IRepository<Corpus> corpora,
    IDataAccessContext dataAccessContext,
    IDataFileService dataFileService,
    IScopedMediator mediator
) : OwnedEntityServiceBase<Corpus>(corpora), ICorpusService
{
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IDataFileService _dataFileService = dataFileService;

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
                await _mediator.Publish(
                    new CorpusUpdated
                    {
                        CorpusId = corpus.Id,
                        Files = await Task.WhenAll(
                            corpus.Files.Select(async f => new CorpusFileResult
                            {
                                TextId = f.TextId!,
                                File = Map(await _dataFileService.GetAsync(f.FileId))
                            })
                        )
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
