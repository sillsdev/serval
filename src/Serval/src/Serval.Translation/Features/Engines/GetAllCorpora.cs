namespace Serval.Translation.Features.Engines;

#pragma warning disable CS0612 // Type or member is obsolete

public record GetAllCorpora(string Owner, string EngineId) : IRequest<GetAllCorporaResponse>;

public record GetAllCorporaResponse(IEnumerable<TranslationCorpusDto> Corpora);

public class GetAllCorporaHandler(IRepository<Engine> engines, DtoMapper mapper)
    : IRequestHandler<GetAllCorpora, GetAllCorporaResponse>
{
    public async Task<GetAllCorporaResponse> HandleAsync(
        GetAllCorpora request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);
        return new(engine.Corpora.Select(c => mapper.Map(request.EngineId, c)));
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get all corpora for a translation engine (obsolete - use parallel corpora instead)
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpora</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine</response>
    /// <response code="404">The engine does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<TranslationCorpusDto>>> GetAllCorporaAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<GetAllCorpora, GetAllCorporaResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllCorporaResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok(response.Corpora);
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
