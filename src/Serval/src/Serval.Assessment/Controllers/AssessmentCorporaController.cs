namespace Serval.Assessment.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/assessment/corpora")]
[OpenApiTag("Assessment")]
[FeatureGate("Assessment")]
public class AssessmentCorporaController(
    IAuthorizationService authService,
    ICorpusService corpusService,
    IUrlService urlService
) : ServalControllerBase(authService)
{
    private readonly ICorpusService _corpusService = corpusService;
    private readonly IUrlService _urlService = urlService;

    /// <summary>
    /// Get all corpora.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpora</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadAssessmentCorpora)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<AssessmentCorpusDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _corpusService.GetAllAsync(Owner, cancellationToken)).Select(Map);
    }

    /// <summary>
    /// Get a corpus.
    /// </summary>
    /// <param name="id">The corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the corpus.</response>
    /// <response code="404">The corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>

    [Authorize(Scopes.ReadAssessmentCorpora)]
    [HttpGet("{id}", Name = "GetAssessmentCorpus")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssessmentCorpusDto>> GetAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Corpus corpus = await _corpusService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(corpus);
        return Ok(Map(corpus));
    }

    /// <summary>
    /// Create a new corpus.
    /// </summary>
    /// <param name="corpusConfig">The corpus configuration (see above)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new corpus</response>
    /// <response code="400">Bad request.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.CreateAssessmentCorpora)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssessmentCorpusDto>> CreateAsync(
        [FromBody] AssessmentCorpusConfigDto corpusConfig,
        [FromServices] IRequestClient<GetDataFile> getDataFileClient,
        CancellationToken cancellationToken
    )
    {
        Corpus corpus = await MapAsync(getDataFileClient, corpusConfig, cancellationToken);
        Corpus updatedCorpus = await _corpusService.CreateAsync(corpus, cancellationToken);
        AssessmentCorpusDto dto = Map(updatedCorpus);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Update a corpus with a new set of files.
    /// </summary>
    /// <remarks>
    /// See creating a new corpus for details of use. Will completely replace corpus' file associations.
    /// Will not affect jobs already queued or running. Will not affect existing resuls until a new job is complete.
    /// </remarks>
    /// <param name="id">The corpus id</param>
    /// <param name="corpusConfig">The corpus configuration</param>
    /// <param name="getDataFileClient">The data file client</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was updated successfully</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the corpus.</response>
    /// <response code="404">The corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateAssessmentCorpora)]
    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssessmentCorpusDto>> UpdateAsync(
        [NotNull] string id,
        [FromBody] AssessmentCorpusUpdateConfigDto corpusConfig,
        [FromServices] IRequestClient<GetDataFile> getDataFileClient,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        Corpus corpus = await _corpusService.UpdateAsync(
            id,
            await MapAsync(getDataFileClient, corpusConfig.Files, cancellationToken),
            cancellationToken
        );
        return Ok(Map(corpus));
    }

    /// <summary>
    /// Delete a corpus.
    /// </summary>
    /// <param name="id">The corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was successfully deleted.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the corpus.</response>
    /// <response code="404">The corpus does not exist and therefore cannot be deleted.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.DeleteAssessmentEngines)]
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

    private AssessmentCorpusDto Map(Corpus source)
    {
        return new AssessmentCorpusDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl("GetAssessmentCorpus", new { id = source.Id }),
            Name = source.Name,
            Language = source.Language,
            Files = source.Files.Select(Map).ToList()
        };
    }

    private AssessmentCorpusFileDto Map(CorpusFile source)
    {
        return new AssessmentCorpusFileDto
        {
            File = new ResourceLinkDto
            {
                Id = source.Id,
                Url = _urlService.GetUrl("GetDataFile", new { id = source.Id })
            },
            TextId = source.TextId
        };
    }

    private async Task<Corpus> MapAsync(
        IRequestClient<GetDataFile> getDataFileClient,
        AssessmentCorpusConfigDto source,
        CancellationToken cancellationToken
    )
    {
        return new Corpus
        {
            Name = source.Name,
            Language = source.Language,
            Owner = Owner,
            Files = await MapAsync(getDataFileClient, source.Files, cancellationToken)
        };
    }

    private async Task<List<CorpusFile>> MapAsync(
        IRequestClient<GetDataFile> getDataFileClient,
        IEnumerable<AssessmentCorpusFileConfigDto> fileConfigs,
        CancellationToken cancellationToken
    )
    {
        var files = new List<CorpusFile>();
        foreach (AssessmentCorpusFileConfigDto fileConfig in fileConfigs)
        {
            Response<DataFileResult, DataFileNotFound> response = await getDataFileClient.GetResponse<
                DataFileResult,
                DataFileNotFound
            >(new GetDataFile { DataFileId = fileConfig.FileId, Owner = Owner }, cancellationToken);
            if (response.Is(out Response<DataFileResult>? result))
            {
                files.Add(
                    new CorpusFile
                    {
                        Id = fileConfig.FileId,
                        Filename = result.Message.Filename,
                        TextId = fileConfig.TextId ?? result.Message.Name,
                        Format = result.Message.Format
                    }
                );
            }
            else if (response.Is(out Response<DataFileNotFound>? _))
            {
                throw new InvalidOperationException($"The data file {fileConfig.FileId} cannot be found.");
            }
        }
        return files;
    }
}
