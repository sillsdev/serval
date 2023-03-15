namespace Serval.Corpora.Controllers;

[Route("files")]
public class DataFilesController : ServalControllerBase
{
    private readonly IDataFileService _dataFileService;
    private readonly IMapper _mapper;

    public DataFilesController(IAuthorizationService authService, IDataFileService dataFileService, IMapper mapper)
        : base(authService)
    {
        _dataFileService = dataFileService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all files.
    /// </summary>
    /// <response code="200">The files.</response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet]
    public async Task<IEnumerable<DataFileDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _dataFileService.GetAllAsync(User.Identity!.Name!, cancellationToken)).Select(
            _mapper.Map<DataFileDto>
        );
    }

    /// <summary>
    /// Gets a file.
    /// </summary>
    /// <param name="id">The file id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The file.</response>
    /// <response code="403">The authenticated client does not own the corpus.</response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DataFileDto>> GetAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        DataFile? dataFile = await _dataFileService.GetAsync(id, cancellationToken);
        if (dataFile == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(dataFile))
            return Forbid();

        return Ok(_mapper.Map<DataFileDto>(dataFile));
    }

    /// <summary>
    /// Uploads a file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="201">The file was uploaded successfully.</response>
    [Authorize(Scopes.CreateFiles)]
    [HttpPost]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<DataFileDto>> CreateAsync(
        [BindRequired] IFormFile file,
        CancellationToken cancellationToken
    )
    {
        var dataFile = new DataFile { Name = file.FileName };
        using (Stream stream = file.OpenReadStream())
        {
            await _dataFileService.CreateAsync(dataFile, stream, cancellationToken);
        }
        var dto = _mapper.Map<DataFileDto>(dataFile);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="id">The file id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The file was deleted successfully.</response>
    /// <response code="403">The authenticated client does not own the corpus.</response>
    [Authorize(Scopes.DeleteFiles)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        DataFile? dataFile = await _dataFileService.GetAsync(id, cancellationToken);
        if (dataFile == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(dataFile))
            return Forbid();

        if (!await _dataFileService.DeleteAsync(id, cancellationToken))
            return NotFound();

        return Ok();
    }
}
