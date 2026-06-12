namespace Serval.WordAlignment.Features.Engines;

public record GetAllBuilds(string Owner, string EngineId) : IRequest<GetAllBuildsResponse>;

public record GetAllBuildsResponse(IEnumerable<WordAlignmentBuildDto> Builds);

public class GetAllBuildsHandler(IRepository<Engine> engines, IRepository<Build> builds, DtoMapper mapper)
    : IRequestHandler<GetAllBuilds, GetAllBuildsResponse>
{
    public async Task<GetAllBuildsResponse> HandleAsync(
        GetAllBuilds request,
        CancellationToken cancellationToken = default
    )
    {
        await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);

        IEnumerable<Build> allBuilds = await builds.GetAllAsync(
            b => b.EngineRef == request.EngineId,
            cancellationToken
        );
        return new(allBuilds.Select(mapper.Map));
    }
}

public partial class WordAlignmentEnginesController
{
    /// <summary>
    /// Get all build jobs for a engine
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build jobs</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<WordAlignmentBuildDto>>> GetAllBuildsAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<GetAllBuilds, GetAllBuildsResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllBuildsResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok(response.Builds);
    }
}
