namespace Serval.DataFiles.Features.Corpora;

public record GetAllCorpora(string Owner) : IRequest<GetAllCorporaResponse>;

public record GetAllCorporaResponse(IEnumerable<CorpusDto> Corpora);

public class GetAllCorporaHandler(IRepository<Corpus> corpora, DtoMapper mapper)
    : IRequestHandler<GetAllCorpora, GetAllCorporaResponse>
{
    public async Task<GetAllCorporaResponse> HandleAsync(GetAllCorpora request, CancellationToken cancellationToken)
    {
        IEnumerable<CorpusDto> dtos = (
            await corpora.GetAllAsync(c => c.Owner == request.Owner, cancellationToken)
        ).Select(mapper.Map);
        return new(dtos);
    }
}

public partial class CorporaController
{
    /// <summary>
    /// Get all corpora
    /// </summary>
    /// <response code="200">A list of all corpora owned by the client</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadFiles)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<CorpusDto>> GetAllAsync(
        [FromServices] IRequestHandler<GetAllCorpora, GetAllCorporaResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllCorporaResponse response = await handler.HandleAsync(new(Owner), cancellationToken);
        return response.Corpora;
    }
}
