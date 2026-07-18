namespace Serval.ApiKeys.Features.ApiKeys;

public record DeleteApiKey(string ApiKeyId) : IRequest;

public class DeleteApiKeyHandler(IDataAccessContext dataAccessContext, IRepository<ApiKey> apiKeys)
    : IRequestHandler<DeleteApiKey>
{
    public async Task HandleAsync(DeleteApiKey request, CancellationToken cancellationToken = default)
    {
        await dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                ApiKey? apiKey = await apiKeys.UpdateAsync(
                    k => k.Id == request.ApiKeyId && k.RevokedAt == null,
                    u => u.Set(k => k.RevokedAt, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (apiKey is null && !await apiKeys.ExistsAsync(k => k.Id == request.ApiKeyId, ct))
                    throw new EntityNotFoundException($"Could not find the ApiKey '{request.ApiKeyId}'.");
            },
            cancellationToken
        );
    }
}

public partial class ApiKeysController
{
    /// <summary>
    /// Revokes an API key.
    /// </summary>
    /// <remarks>
    /// The API key is marked as revoked and can no longer be used to authenticate. The key record is retained and
    /// can still be retrieved. Revoking an API key that is already revoked has no effect.
    /// </remarks>
    /// <param name="id">The API key id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The API key was successfully revoked.</response>
    /// <response code="404">The API key does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.DeleteApiKeys)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<DeleteApiKey> handler,
        CancellationToken cancellationToken
    )
    {
        await handler.HandleAsync(new(id), cancellationToken);
        return Ok();
    }
}
