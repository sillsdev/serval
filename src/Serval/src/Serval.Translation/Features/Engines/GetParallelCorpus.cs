namespace Serval.Translation.Features.Engines;

public record GetParallelCorpus(string Owner, string EngineId, string ParallelCorpusId)
    : IRequest<GetParallelCorpusResponse>;

public record GetParallelCorpusResponse(TranslationParallelCorpusDto? Corpus);

public class GetParallelCorpusHandler(IRepository<Engine> engines, DtoMapper mapper)
    : IRequestHandler<GetParallelCorpus, GetParallelCorpusResponse>
{
    public async Task<GetParallelCorpusResponse> HandleAsync(
        GetParallelCorpus request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);
        ParallelCorpus? corpus = engine.ParallelCorpora.FirstOrDefault(c => c.Id == request.ParallelCorpusId);
        return new(corpus is null ? null : mapper.Map(request.EngineId, corpus));
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get the configuration of a parallel corpus for a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The parallel corpus configuration</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/parallel-corpora/{parallelCorpusId}", Name = Endpoints.GetParallelTranslationCorpus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationParallelCorpusDto>> GetParallelCorpusAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        [FromServices] IRequestHandler<GetParallelCorpus, GetParallelCorpusResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetParallelCorpusResponse response = await handler.HandleAsync(
            new(Owner, id, parallelCorpusId),
            cancellationToken
        );
        if (response.Corpus is null)
            return NotFound();
        return Ok(response.Corpus);
    }
}
