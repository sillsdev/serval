namespace Serval.DataFiles.Features.Corpora;

public record UpdateCorpus(string Owner, string CorpusId, IReadOnlyList<CorpusFileConfigDto> Files)
    : IRequest<UpdateCorpusResponse>;

public record UpdateCorpusResponse(CorpusDto Corpus);

public class UpdateCorpusHandler(
    IRepository<Corpus> corpora,
    IRepository<DataFile> dataFiles,
    IDataAccessContext dataAccessContext,
    IEventRouter eventRouter,
    DtoMapper mapper
) : IRequestHandler<UpdateCorpus, UpdateCorpusResponse>
{
    public async Task<UpdateCorpusResponse> HandleAsync(UpdateCorpus request, CancellationToken cancellationToken)
    {
        await corpora.CheckOwnerAsync(request.CorpusId, request.Owner, cancellationToken);

        IReadOnlyList<CorpusFile> files = await MapFilesAsync(request.Files, cancellationToken);

        Corpus corpus = await dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                Corpus? updated = await corpora.UpdateAsync(
                    c => c.Id == request.CorpusId,
                    u => u.Set(c => c.Files, files),
                    cancellationToken: ct
                );
                if (updated is null)
                    throw new EntityNotFoundException($"Could not find the Corpus '{request.CorpusId}'.");
                HashSet<string> corpusFileIds = updated.Files.Select(f => f.FileRef).ToHashSet();
                IDictionary<string, DataFile> corpusDataFilesDict = (
                    await dataFiles.GetAllAsync(f => corpusFileIds.Contains(f.Id), ct)
                ).ToDictionary(f => f.Id);
                await eventRouter.PublishAsync(
                    new CorpusUpdated(
                        updated.Id,
                        [
                            .. updated.Files.Select(f => new CorpusDataFileContract(
                                File: MapDataFile(corpusDataFilesDict[f.FileRef]),
                                f.TextId ?? corpusDataFilesDict[f.FileRef].Name
                            )),
                        ]
                    ),
                    ct
                );
                return updated;
            },
            cancellationToken
        );

        return new(mapper.Map(corpus));
    }

    private async Task<IReadOnlyList<CorpusFile>> MapFilesAsync(
        IReadOnlyList<CorpusFileConfigDto> files,
        CancellationToken cancellationToken
    )
    {
        var corpusFiles = new List<CorpusFile>();
        foreach (CorpusFileConfigDto file in files)
        {
            DataFile? dataFile = await dataFiles.GetAsync(file.FileId, cancellationToken);
            if (dataFile is null)
                throw new EntityNotFoundException($"Could not find the DataFile '{file.FileId}'.");
            corpusFiles.Add(new CorpusFile { FileRef = file.FileId, TextId = file.TextId });
        }
        return corpusFiles;
    }

    private static DataFileContract MapDataFile(DataFile dataFile) =>
        new(dataFile.Id, dataFile.Name, dataFile.Filename, dataFile.Format);
}

public partial class CorporaController
{
    /// <summary>
    /// Update an existing corpus
    /// </summary>
    /// <param name="id">The unique identifier for the corpus</param>
    /// <param name="files">Tuples of the ids of the new corpus files and the associated text ids</param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was updated successfully</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the corpus</response>
    /// <response code="404">The corpus does not exist and therefore cannot be updated</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.UpdateFiles)]
    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CorpusDto>> UpdateAsync(
        [NotNull] string id,
        [NotNull] IReadOnlyList<CorpusFileConfigDto> files,
        [FromServices] IRequestHandler<UpdateCorpus, UpdateCorpusResponse> handler,
        CancellationToken cancellationToken
    )
    {
        UpdateCorpusResponse response = await handler.HandleAsync(new(Owner, id, files), cancellationToken);
        return Ok(response.Corpus);
    }
}
