namespace Serval.Translation.Features.Engines;

public record GetAllParallelCorpora(string Owner, string EngineId) : IRequest<GetAllParallelCorporaResponse>;

public record GetAllParallelCorporaResponse(IEnumerable<TranslationParallelCorpusDto> Corpora);

public class GetAllParallelCorporaHandler(IRepository<Engine> engines, DtoMapper mapper)
    : IRequestHandler<GetAllParallelCorpora, GetAllParallelCorporaResponse>
{
    public async Task<GetAllParallelCorporaResponse> HandleAsync(
        GetAllParallelCorpora request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);
        return new(engine.ParallelCorpora.Select(c => mapper.Map(request.EngineId, c)));
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get all parallel corpora for a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The parallel corpora</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine</response>
    /// <response code="404">The engine does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/parallel-corpora")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<TranslationParallelCorpusDto>>> GetAllParallelCorporaAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<GetAllParallelCorpora, GetAllParallelCorporaResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllParallelCorporaResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok(response.Corpora);
    }
}
