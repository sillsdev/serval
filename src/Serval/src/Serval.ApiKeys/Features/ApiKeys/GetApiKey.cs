namespace Serval.ApiKeys.Features.ApiKeys;

public record GetApiKey(string ApiKeyId) : IRequest<GetApiKeyResponse>;

public record GetApiKeyResponse(ApiKeyDto ApiKey);

public class GetApiKeyHandler(IRepository<ApiKey> apiKeys, DtoMapper mapper)
    : IRequestHandler<GetApiKey, GetApiKeyResponse>
{
    public async Task<GetApiKeyResponse> HandleAsync(GetApiKey request, CancellationToken cancellationToken = default)
    {
        ApiKey? apiKey = await apiKeys.GetAsync(request.ApiKeyId, cancellationToken);
        if (apiKey is null)
            throw new EntityNotFoundException($"Could not find the ApiKey '{request.ApiKeyId}'.");
        return new(mapper.Map(apiKey));
    }
}

public partial class ApiKeysController
{
    /// <summary>
    /// Gets an API key.
    /// </summary>
    /// <remarks>
    /// The plaintext API key is not returned. It is only available when the API key is created.
    /// </remarks>
    /// <param name="id">The API key id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The API key.</response>
    /// <response code="404">The API key does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadApiKeys)]
    [HttpGet("{id}", Name = Endpoints.GetApiKey)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiKeyDto>> GetAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<GetApiKey, GetApiKeyResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetApiKeyResponse response = await handler.HandleAsync(new(id), cancellationToken);
        return Ok(response.ApiKey);
    }
}
