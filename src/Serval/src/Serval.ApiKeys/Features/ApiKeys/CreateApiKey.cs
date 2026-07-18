namespace Serval.ApiKeys.Features.ApiKeys;

public record ApiKeyConfigDto
{
    /// <summary>
    /// The client identity that the API key acts as.
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// A human-readable name for the API key.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The scopes granted to the API key.
    /// </summary>
    public required IList<string> Scopes { get; init; }

    /// <summary>
    /// The optional expiration date and time (either in UTC or with offset) of the API key.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }
}

public record CreateApiKey(ApiKeyConfigDto ApiKeyConfig) : IRequest<CreateApiKeyResponse>;

public record CreateApiKeyResponse(ApiKeyCreatedDto ApiKey);

public class CreateApiKeyHandler(IApiKeyService apiKeyService, DtoMapper mapper)
    : IRequestHandler<CreateApiKey, CreateApiKeyResponse>
{
    public async Task<CreateApiKeyResponse> HandleAsync(
        CreateApiKey request,
        CancellationToken cancellationToken = default
    )
    {
        ApiKeyConfigDto config = request.ApiKeyConfig;
        if (string.IsNullOrWhiteSpace(config.Owner))
            throw new InvalidOperationException("The owner must not be empty.");
        if (string.IsNullOrWhiteSpace(config.Name))
            throw new InvalidOperationException("The name must not be empty.");
        if (config.Scopes.Count == 0)
            throw new InvalidOperationException("At least one scope must be specified.");
        string[] invalidScopes = [.. config.Scopes.Except(Scopes.Grantable)];
        if (invalidScopes.Length > 0)
        {
            throw new InvalidOperationException(
                $"The scopes '{string.Join("', '", invalidScopes)}' cannot be granted to an API key."
            );
        }
        DateTime? expiresAt = config.ExpiresAt;
        if (expiresAt is not null)
        {
            expiresAt =
                expiresAt.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc)
                    : expiresAt.Value.ToUniversalTime();
        }
        if (expiresAt is not null && expiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("The expiration date must be in the future.");

        (ApiKey apiKey, string key) = await apiKeyService.CreateAsync(
            config.Owner,
            config.Name,
            config.Scopes,
            expiresAt,
            cancellationToken
        );
        return new CreateApiKeyResponse(mapper.Map(apiKey, key));
    }
}

public partial class ApiKeysController
{
    /// <summary>
    /// Creates a new API key.
    /// </summary>
    /// <remarks>
    /// The plaintext API key is only returned in the response of this endpoint and cannot be retrieved afterwards.
    ///
    /// API key management is intended for administrative use only. A key can be created for any owner with any
    /// grantable scopes, so the `create:api_keys`, `read:api_keys`, and `delete:api_keys` scopes should only be
    /// granted to trusted administrative clients.
    /// </remarks>
    /// <param name="apiKeyConfig">The API key configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="201">The API key was created successfully.</response>
    /// <response code="400">The API key configuration is invalid.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.CreateApiKeys)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiKeyCreatedDto>> CreateAsync(
        [FromBody] ApiKeyConfigDto apiKeyConfig,
        [FromServices] IRequestHandler<CreateApiKey, CreateApiKeyResponse> handler,
        CancellationToken cancellationToken
    )
    {
        CreateApiKeyResponse response = await handler.HandleAsync(new(apiKeyConfig), cancellationToken);
        return Created(response.ApiKey.Url, response.ApiKey);
    }
}
