namespace Serval.Translation.Features.Engines;

#pragma warning disable CS0612 // Type or member is obsolete

public record GetTranslationCorpus(string Owner, string EngineId, string CorpusId)
    : IRequest<GetTranslationCorpusResponse>;

public record GetTranslationCorpusResponse(TranslationCorpusDto? Corpus);

public class GetTranslationCorpusHandler(IRepository<Engine> engines, DtoMapper mapper)
    : IRequestHandler<GetTranslationCorpus, GetTranslationCorpusResponse>
{
    public async Task<GetTranslationCorpusResponse> HandleAsync(
        GetTranslationCorpus request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);
        Corpus? corpus = engine.Corpora.FirstOrDefault(c => c.Id == request.CorpusId);
        return new(corpus is null ? null : mapper.Map(request.EngineId, corpus));
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get the configuration of a corpus for a translation engine (obsolete - use parallel corpora instead)
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus configuration</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}", Name = Endpoints.GetTranslationCorpus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationCorpusDto>> GetCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromServices] IRequestHandler<GetTranslationCorpus, GetTranslationCorpusResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetTranslationCorpusResponse response = await handler.HandleAsync(new(Owner, id, corpusId), cancellationToken);
        if (response.Corpus is null)
            return NotFound();
        return Ok(response.Corpus);
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
