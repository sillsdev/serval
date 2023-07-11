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
    /// Get all files
    /// </summary>
    /// <response code="200">A list of all files owned by the client</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<IEnumerable<DataFileDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _dataFileService.GetAllAsync(Owner, cancellationToken)).Select(Map);
    }

    /// <summary>
    /// Get a file by unique id
    /// </summary>
    /// <param name="id">The unique identifier for the file</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The file exists</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the file</response>
    /// <response code="404">The file does not exist</response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet("{id}", Name = "GetDataFile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
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
    /// Upload a new file
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /files
    ///     {
    ///        "format": "text",
    ///        "name": "myTeam:myProject:myFile.txt"
    ///     }
    ///
    /// </remarks>
    /// <param name="file">
    /// The file to upload.  Max size: 100MB
    /// </param>
    /// <param name="name">
    /// A name to help identify and distinguish the file.
    /// Recommendation: Create a multi-part name to distinguish between projects, uses, languages, etc.
    /// The name does not have to be unique.
    /// Example: myTranslationTeam:myProject:myLanguage:myFile.txt
    /// </param>
    /// <param name="format">
    /// File format options:
    /// * **Text**: One translation unit (a.k.a., verse) per line
    ///   * If there is a tab, the content before the tab is the unique identifier for the line
    ///   * Otherwise, no tabs should be used in the file.
    /// * **Paratext**: A complete, zipped Paratext project
    /// </param>
    /// <param name="idGenerator"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The file was created successfully</response>
    /// <response code="400">Bad request. Is the file over 100 MB?</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    [Authorize(Scopes.CreateFiles)]
    [HttpPost]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DataFileDto>> CreateAsync(
        [BindRequired] IFormFile file,
        [BindRequired, FromForm] FileFormat format,
        [FromServices] IIdGenerator idGenerator,
        [FromForm] string? name,
        CancellationToken cancellationToken
    )
    {
        var dataFile = new DataFile
        {
            Id = idGenerator.GenerateId(),
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
    /// Update an existing file
    /// </summary>
    /// <param name="id">The existing file's unique id</param>
    /// <param name="file">The updated file</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The file was updated successfully</response>
    /// <response code="400">Bad request. Is the file over 100 MB?</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the file</response>
    /// <response code="404">The file does not exist and therefore cannot be updated</response>
    [Authorize(Scopes.UpdateFiles)]
    [HttpPatch("{id}")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
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
    /// Delete an existing file
    /// </summary>
    /// <remarks>
    /// If a file is in a corpora and the file is deleted, it will be automatically removed from the corpora.
    /// If a build job has started before the file was deleted, the file will be used for the build job, even
    /// though it will no longer be accessible through the API.
    /// </remarks>
    /// <param name="id">The existing file's unique id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The file was deleted successfully</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the file</response>
    /// <response code="404">The file does not exist and therefore cannot be deleted</response>
    [Authorize(Scopes.DeleteFiles)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
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
