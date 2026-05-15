namespace Serval.DataFiles.Features.DataFiles;

public record GetAllDataFiles(string Owner) : IRequest<GetAllDataFilesResponse>;

public record GetAllDataFilesResponse(IEnumerable<DataFileDto> DataFiles);

public class GetAllDataFilesHandler(IRepository<DataFile> dataFiles, DtoMapper mapper)
    : IRequestHandler<GetAllDataFiles, GetAllDataFilesResponse>
{
    public async Task<GetAllDataFilesResponse> HandleAsync(GetAllDataFiles request, CancellationToken cancellationToken)
    {
        IEnumerable<DataFileDto> dtos = (
            await dataFiles.GetAllAsync(f => f.Owner == request.Owner, cancellationToken)
        ).Select(mapper.Map);
        return new(dtos);
    }
}

public partial class DataFilesController
{
    /// <summary>
    /// Get all files
    /// </summary>
    /// <response code="200">A list of all files owned by the client</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<DataFileDto>> GetAllAsync(
        [FromServices] IRequestHandler<GetAllDataFiles, GetAllDataFilesResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllDataFilesResponse response = await handler.HandleAsync(new(Owner), cancellationToken);
        return response.DataFiles;
    }
}
