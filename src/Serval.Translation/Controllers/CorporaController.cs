namespace Serval.Corpora.Controllers;

[Route("translation/corpora")]
public class CorporaController : ServalControllerBase
{
    private readonly ICorpusService _corpusService;
    private readonly IMapper _mapper;

    public CorporaController(IAuthorizationService authService, ICorpusService corpusService, IMapper mapper)
        : base(authService)
    {
        _corpusService = corpusService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all corpora.
    /// </summary>
    /// <response code="200">The corpora.</response>
    [Authorize(Scopes.ReadTranslationCorpora)]
    [HttpGet]
    public async Task<IEnumerable<CorpusDto>> GetAllAsync()
    {
        return (await _corpusService.GetAllAsync(User.Identity!.Name!)).Select(_mapper.Map<CorpusDto>);
    }

    /// <summary>
    /// Gets a corpus.
    /// </summary>
    /// <param name="id">The corpus id.</param>
    /// <response code="200">The corpus.</response>
    /// <response code="403">The authenticated client does not own the corpus.</response>
    [Authorize(Scopes.ReadTranslationCorpora)]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CorpusDto>> GetAsync([NotNull] string id)
    {
        Corpus? corpus = await _corpusService.GetAsync(id);
        if (corpus == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(corpus))
            return Forbid();

        return Ok(_mapper.Map<CorpusDto>(corpus));
    }

    /// <summary>
    /// Creates a new corpus.
    /// </summary>
    /// <param name="corpusConfig">The corpus configuration.</param>
    /// <response code="201">The corpus was created successfully.</response>
    [Authorize(Scopes.CreateTranslationCorpora)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<CorpusDto>> CreateAsync([FromBody] CorpusConfigDto corpusConfig)
    {
        var newCorpus = new Corpus
        {
            Name = corpusConfig.Name,
            Type = corpusConfig.Type,
            Format = corpusConfig.Format,
            Owner = User.Identity!.Name!
        };

        await _corpusService.CreateAsync(newCorpus);
        var dto = _mapper.Map<CorpusDto>(newCorpus);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Deletes a corpus.
    /// </summary>
    /// <param name="id">The corpus id.</param>
    /// <response code="200">The corpus was successfully deleted.</response>
    /// <response code="403">The authenticated client does not own the corpus.</response>
    [Authorize(Scopes.DeleteTranslationCorpora)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteAsync([NotNull] string id)
    {
        Corpus? corpus = await _corpusService.GetAsync(id);
        if (corpus == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(corpus))
            return Forbid();

        if (!await _corpusService.DeleteAsync(id))
            return NotFound();
        return Ok();
    }

    /// <summary>
    /// Uploads a data file to a corpus.
    /// </summary>
    /// <param name="id">The corpus id.</param>
    /// <param name="dataFileConfig">The file configuration.</param>
    /// <response code="201">The data file was uploaded successfully.</response>
    [Authorize(Scopes.UpdateTranslationCorpora)]
    [HttpPost("{id}/files")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<CorpusFileDto>> AddFileAsync(
        [NotNull] string id,
        [FromBody] CorpusFileConfigDto dataFileConfig
    )
    {
        Corpus? corpus = await _corpusService.GetAsync(id);
        if (corpus == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(corpus))
            return Forbid();

        var dataFile = new CorpusFile
        {
            DataFileRef = dataFileConfig.FileId,
            LanguageTag = dataFileConfig.LanguageTag,
            TextId = dataFileConfig.TextId
        };
        await _corpusService.AddDataFileAsync(id, dataFile);
        var dto = Map(id, dataFile);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Gets all files for a corpus.
    /// </summary>
    /// <param name="id">The corpus id.</param>
    /// <response code="200">The files.</response>
    /// <response code="403">The authenticated client does not own the corpus.</response>
    [Authorize(Scopes.ReadTranslationCorpora)]
    [HttpGet("{id}/files")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<CorpusFileDto>>> GetAllFilesAsync([NotNull] string id)
    {
        Corpus? corpus = await _corpusService.GetAsync(id);
        if (corpus == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(corpus))
            return Forbid();

        return Ok(corpus.Files.Select(f => Map(id, f)));
    }

    /// <summary>
    /// Gets a file for a corpus.
    /// </summary>
    /// <param name="id">The corpus id.</param>
    /// <param name="fileId">The file id.</param>
    /// <response code="200">The file.</response>
    /// <response code="403">The authenticated client does not own the corpus.</response>
    [Authorize(Scopes.ReadTranslationCorpora)]
    [HttpGet("{id}/files/{fileId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CorpusFileDto>> GetFileAsync([NotNull] string id, [NotNull] string fileId)
    {
        Corpus? corpus = await _corpusService.GetAsync(id);
        if (corpus == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(corpus))
            return Forbid();

        CorpusFile? dataFile = corpus.Files.FirstOrDefault(f => f.Id == fileId);
        if (dataFile == null)
            return NotFound();

        return Ok(Map(id, dataFile));
    }

    /// <summary>
    /// Deletes a file from a corpus.
    /// </summary>
    /// <param name="id">The corpus id.</param>
    /// <param name="fileId">The file id.</param>
    /// <response code="200">The file was deleted successfully.</response>
    /// <response code="403">The authenticated client does not own the corpus.</response>
    [Authorize(Scopes.UpdateTranslationCorpora)]
    [HttpDelete("{id}/files/{fileId}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteFileAsync([NotNull] string id, [NotNull] string fileId)
    {
        Corpus? corpus = await _corpusService.GetAsync(id);
        if (corpus == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(corpus))
            return Forbid();

        if (!await _corpusService.DeleteDataFileAsync(id, fileId))
            return NotFound();

        return Ok();
    }

    private CorpusFileDto Map(string corpusId, CorpusFile file)
    {
        return _mapper.Map<CorpusFileDto>(file, opts => opts.Items["CorpusId"] = corpusId);
    }
}
