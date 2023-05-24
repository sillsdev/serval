namespace Serval.DataFiles.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/files")]
[OpenApiTag("Files")]
public class DataFilesController : ServalControllerBase
{
    private readonly IDataFileService _dataFileService;
    private readonly IUrlService _urlService;

    public DataFilesController(
        IAuthorizationService authService,
        IDataFileService dataFileService,
        IUrlService urlService
    )
        : base(authService)
    {
        _dataFileService = dataFileService;
        _urlService = urlService;
    }

    /// <summary>
    /// Gets all files.
    /// </summary>
    /// <response code="200">The files.</response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet]
    public async Task<IEnumerable<DataFileDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _dataFileService.GetAllAsync(Owner, cancellationToken)).Select(Map);
    }

    /// <summary>
    /// Gets a file.
    /// </summary>
    /// <param name="id">The file id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The file.</response>
    /// <response code="403">The authenticated client does not own the file.</response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet("{id}", Name = "GetDataFile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DataFileDto>> GetAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        DataFile? dataFile = await _dataFileService.GetAsync(id, cancellationToken);
        if (dataFile == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(dataFile))
            return Forbid();

        return Ok(Map(dataFile));
    }

    /// <summary>
    /// Creates a new file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="name">The name.</param>
    /// <param name="format">The file format.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="201">The file was created successfully.</response>
    [Authorize(Scopes.CreateFiles)]
    [HttpPost]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<DataFileDto>> CreateAsync(
        [BindRequired] IFormFile file,
        [BindRequired, FromForm] FileFormat format,
        [FromForm] string? name,
        CancellationToken cancellationToken
    )
    {
        var dataFile = new DataFile
        {
            Name = name ?? file.FileName,
            Format = format,
            Owner = Owner
        };
        using (Stream stream = file.OpenReadStream())
        {
            await _dataFileService.CreateAsync(dataFile, stream, cancellationToken);
        }
        var dto = Map(dataFile);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Updates a file.
    /// </summary>
    /// <param name="id">The file id.</param>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The file was updated successfully.</response>
    /// <response code="403">The authenticated client does not own the file.</response>
    [Authorize(Scopes.UpdateFiles)]
    [HttpPatch("{id}")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DataFileDto>> UpdateAsync(
        [NotNull] string id,
        [BindRequired] IFormFile file,
        CancellationToken cancellationToken
    )
    {
        DataFile? dataFile = await _dataFileService.GetAsync(id, cancellationToken);
        if (dataFile == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(dataFile))
            return Forbid();

        using (Stream stream = file.OpenReadStream())
            dataFile = await _dataFileService.UpdateAsync(id, stream, cancellationToken);
        if (dataFile is null)
            return NotFound();

        var dto = Map(dataFile);
        return Ok(dto);
    }

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="id">The file id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The file was deleted successfully.</response>
    /// <response code="403">The authenticated client does not own the file.</response>
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

    private DataFileDto Map(DataFile source)
    {
        return new DataFileDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl("GetDataFile", new { id = source.Id }),
            Name = source.Name,
            Format = source.Format,
            Revision = source.Revision
        };
    }
}
