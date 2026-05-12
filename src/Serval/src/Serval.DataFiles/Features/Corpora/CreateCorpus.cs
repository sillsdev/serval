namespace Serval.DataFiles.Features.Corpora;

public record CorpusConfigDto
{
    public string? Name { get; init; }

    public required string Language { get; init; }

    public required IReadOnlyList<CorpusFileConfigDto> Files { get; init; }
}

public record CreateCorpus(string Owner, CorpusConfigDto CorpusConfig) : IRequest<CreateCorpusResponse>;

public record CreateCorpusResponse(CorpusDto Corpus);

public class CreateCorpusHandler(
    IRepository<Corpus> corpora,
    IRepository<DataFile> dataFiles,
    IIdGenerator idGenerator,
    DtoMapper mapper
) : IRequestHandler<CreateCorpus, CreateCorpusResponse>
{
    public async Task<CreateCorpusResponse> HandleAsync(CreateCorpus request, CancellationToken cancellationToken)
    {
        if (request.CorpusConfig.Language.Length == 0)
            throw new InvalidOperationException("Corpus must have a language.");
        Corpus corpus = new()
        {
            Id = idGenerator.GenerateId(),
            Owner = request.Owner,
            Name = request.CorpusConfig.Name,
            Language = request.CorpusConfig.Language,
            Files = await MapFilesAsync(request.CorpusConfig.Files, cancellationToken),
        };
        await corpora.InsertAsync(corpus, cancellationToken);
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
}

public partial class CorporaController
{
    /// <summary>
    /// Create a new corpus
    /// </summary>
    /// <param name="corpusConfig">The corpus configuration</param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The corpus was created successfully</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.CreateFiles)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CorpusDto>> CreateAsync(
        [FromBody] CorpusConfigDto corpusConfig,
        [FromServices] IRequestHandler<CreateCorpus, CreateCorpusResponse> handler,
        CancellationToken cancellationToken
    )
    {
        CreateCorpusResponse response = await handler.HandleAsync(new(Owner, corpusConfig), cancellationToken);
        return Created(response.Corpus.Url, response.Corpus);
    }
}
