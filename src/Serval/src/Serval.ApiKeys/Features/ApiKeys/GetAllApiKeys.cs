namespace Serval.ApiKeys.Features.ApiKeys;

public record GetAllApiKeys(string? ClientId) : IRequest<GetAllApiKeysResponse>;

public record GetAllApiKeysResponse(IEnumerable<ApiKeyDto> ApiKeys);

public class GetAllApiKeysHandler(IRepository<ApiKey> apiKeys, DtoMapper mapper)
    : IRequestHandler<GetAllApiKeys, GetAllApiKeysResponse>
{
    public async Task<GetAllApiKeysResponse> HandleAsync(
        GetAllApiKeys request,
        CancellationToken cancellationToken = default
    )
    {
        IEnumerable<ApiKeyDto> dtos = (
            await apiKeys.GetAllAsync(
                k => request.ClientId == null || k.ClientId == request.ClientId,
                cancellationToken
            )
        ).Select(mapper.Map);
        return new(dtos);
    }
}

public partial class ApiKeysController
{
    /// <summary>
    /// Gets all API keys.
    /// </summary>
    /// <remarks>
    /// The plaintext API keys are not returned. They are only available when the API keys are created. Revoked API
    /// keys are included.
    /// </remarks>
    /// <param name="clientId">The client id to filter the API keys by. If not specified, all API keys are returned.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The API keys.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadApiKeys)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<ApiKeyDto>> GetAllAsync(
        [FromQuery] string? clientId,
        [FromServices] IRequestHandler<GetAllApiKeys, GetAllApiKeysResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllApiKeysResponse response = await handler.HandleAsync(new(clientId), cancellationToken);
        return response.ApiKeys;
    }
}
