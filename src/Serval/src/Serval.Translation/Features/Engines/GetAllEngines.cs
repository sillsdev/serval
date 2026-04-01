namespace Serval.Translation.Features.Engines;

public record GetAllEngines(string Owner) : IRequest<GetAllEnginesResponse>;

public record GetAllEnginesResponse(IEnumerable<TranslationEngineDto> Engines);

public class GetAllEnginesHandler(IRepository<Engine> engines, DtoMapper mapper)
    : IRequestHandler<GetAllEngines, GetAllEnginesResponse>
{
    public async Task<GetAllEnginesResponse> HandleAsync(GetAllEngines request, CancellationToken cancellationToken)
    {
        IEnumerable<TranslationEngineDto> dtos = (
            await engines.GetAllAsync(e => e.Owner == request.Owner, cancellationToken)
        ).Select(mapper.Map);
        return new(dtos);
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get all translation engines
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engines</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<TranslationEngineDto>> GetAllAsync(
        [FromServices] IRequestHandler<GetAllEngines, GetAllEnginesResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllEnginesResponse response = await handler.HandleAsync(new(Owner), cancellationToken);
        return response.Engines;
    }
}
