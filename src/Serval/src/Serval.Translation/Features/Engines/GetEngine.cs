namespace Serval.Translation.Features.Engines;

public record GetEngine(string Owner, string EngineId) : IRequest<GetEngineResponse>;

public record GetEngineResponse(TranslationEngineDto Engine);

public class GetEngineHandler(IRepository<Engine> engines, DtoMapper mapper)
    : IRequestHandler<GetEngine, GetEngineResponse>
{
    public async Task<GetEngineResponse> HandleAsync(GetEngine request, CancellationToken cancellationToken)
    {
        Engine? engine = await engines.GetAsync(request.EngineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");
        if (engine.Owner != request.Owner)
            throw new ForbiddenException();
        return new(mapper.Map(engine));
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get a translation engine by unique id
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The translation engine</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}", Name = Endpoints.GetTranslationEngine)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationEngineDto>> GetAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<GetEngine, GetEngineResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetEngineResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok(response.Engine);
    }
}
