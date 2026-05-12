namespace Serval.DataFiles.Features.Corpora;

public record GetCorpus(string Owner, string CorpusId) : IRequest<GetCorpusResponse>;

public record GetCorpusResponse(CorpusDto Corpus);

public class GetCorpusHandler(IRepository<Corpus> corpora, DtoMapper mapper)
    : IRequestHandler<GetCorpus, GetCorpusResponse>
{
    public async Task<GetCorpusResponse> HandleAsync(GetCorpus request, CancellationToken cancellationToken)
    {
        Corpus corpus = await corpora.CheckOwnerAsync(request.CorpusId, request.Owner, cancellationToken);
        return new(mapper.Map(corpus));
    }
}

public partial class CorporaController
{
    /// <summary>
    /// Get a corpus by unique id
    /// </summary>
    /// <param name="id">The unique identifier for the corpus</param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus exists</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the corpus</response>
    /// <response code="404">The corpus does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet("{id}", Name = Endpoints.GetCorpus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CorpusDto>> GetAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<GetCorpus, GetCorpusResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetCorpusResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok(response.Corpus);
    }
}
