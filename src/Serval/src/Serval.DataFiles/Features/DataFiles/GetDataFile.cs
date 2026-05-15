namespace Serval.DataFiles.Features.DataFiles;

public record GetDataFile(string Owner, string FileId) : IRequest<GetDataFileResponse>;

public record GetDataFileResponse(DataFileDto DataFile);

public class GetDataFileHandler(IRepository<DataFile> dataFiles, DtoMapper mapper)
    : IRequestHandler<GetDataFile, GetDataFileResponse>
{
    public async Task<GetDataFileResponse> HandleAsync(GetDataFile request, CancellationToken cancellationToken)
    {
        DataFile dataFile = await dataFiles.CheckOwnerAsync(request.FileId, request.Owner, cancellationToken);
        return new(mapper.Map(dataFile));
    }
}

public partial class DataFilesController
{
    /// <summary>
    /// Get a file by unique id
    /// </summary>
    /// <param name="id">The unique identifier for the file</param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The file exists</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the file</response>
    /// <response code="404">The file does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet("{id}", Name = Endpoints.GetDataFile)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DataFileDto>> GetAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<GetDataFile, GetDataFileResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetDataFileResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok(response.DataFile);
    }
}
