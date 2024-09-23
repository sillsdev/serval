namespace Serval.DataFiles.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/corpora")]
[OpenApiTag("Corpora")]
public class CorporaController(
    IAuthorizationService authService,
    ICorpusService corpusService,
    IUrlService urlService,
    IDataFileService dataFileService
) : ServalControllerBase(authService)
{
    private readonly ICorpusService _corpusService = corpusService;
    private readonly IUrlService _urlService = urlService;

    private readonly IDataFileService _dataFileService = dataFileService;

    /// <summary>
    /// Get all corpora
    /// </summary>
    /// <response code="200">A list of all corpora owned by the client</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<CorpusDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _corpusService.GetAllAsync(Owner, cancellationToken)).Select(Map);
    }

    /// <summary>
    /// Get a corpus by unique id
    /// </summary>
    /// <param name="id">The unique identifier for the corpus</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus exists</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the corpus</response>
    /// <response code="404">The corpus does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet("{id}", Name = Endpoints.GetCorpus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CorpusDto>> GetAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        Corpus corpus = await _corpusService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(corpus);
        return Ok(Map(corpus));
    }

    /// <summary>
    /// Create a new corpus
    /// </summary>
    /// <param name="idGenerator"></param>
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
        [FromServices] IRequestClient<GetDataFile> getDataFileClient,
        [FromServices] IIdGenerator idGenerator,
        CancellationToken cancellationToken
    )
    {
        Corpus corpus = await MapAsync(corpusConfig, idGenerator.GenerateId(), cancellationToken);
        await _corpusService.CreateAsync(corpus, cancellationToken);
        CorpusDto dto = Map(corpus);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Update an existing corpus
    /// </summary>
    /// <param name="id">The unique identifier for the corpus</param>
    /// <param name="files">Tuples of the ids of the new corpus files and the associated text ids</param>
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
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);

        Corpus corpus = await _corpusService.UpdateAsync(
            id,
            await MapAsync(files, cancellationToken),
            cancellationToken
        );

        CorpusDto dto = Map(corpus);
        return Ok(dto);
    }

    /// <summary>
    /// Delete an existing corpus
    /// </summary>
    /// <param name="id">The unique identifier for the corpus</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was deleted successfully</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the corpus</response>
    /// <response code="404">The corpus does not exist and therefore cannot be deleted</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.DeleteFiles)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(id, cancellationToken);
        await _corpusService.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    private async Task AuthorizeAsync(string id, CancellationToken cancellationToken)
    {
        Corpus corpus = await _corpusService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(corpus);
    }

    private async Task<Corpus> MapAsync(CorpusConfigDto corpusConfig, string id, CancellationToken cancellationToken)
    {
        return new Corpus
        {
            Id = id,
            Owner = Owner,
            Language = corpusConfig.Language,
            Files = await MapAsync(corpusConfig.Files, cancellationToken)
        };
    }

    private async Task<IReadOnlyList<CorpusFile>> MapAsync(
        IReadOnlyList<CorpusFileConfigDto> files,
        CancellationToken cancellationToken
    )
    {
        var dataFiles = new List<CorpusFile>();
        foreach (CorpusFileConfigDto file in files)
        {
            DataFile? dataFile = await _dataFileService.GetAsync(file.FileId, cancellationToken);
            if (dataFile == null)
                throw new InvalidOperationException($"DataFile with id {file.FileId} does not exist.");
            dataFiles.Add(new CorpusFile { File = dataFile, TextId = file.TextId });
        }
        return dataFiles;
    }

    private CorpusDto Map(Corpus source)
    {
        return new CorpusDto
        {
            Id = source.Id,
            Language = source.Language,
            Url = _urlService.GetUrl(Endpoints.GetCorpus, new { id = source.Id }),
            Name = source.Name,
            Revision = source.Revision,
            Files = source.Files.Select(Map).ToList()
        };
    }

    private CorpusFileDto Map(CorpusFile source)
    {
        return new CorpusFileDto { File = Map(source.File), TextId = source.TextId };
    }

    private DataFileDto Map(DataFile source)
    {
        return new DataFileDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(Endpoints.GetDataFile, new { id = source.Id }),
            Name = source.Name,
            Format = source.Format,
            Revision = source.Revision
        };
    }
}
