namespace Serval.ApiKeys.Features.ApiKeys;

public record DeleteApiKey(string ApiKeyId) : IRequest;

public class DeleteApiKeyHandler(IRepository<ApiKey> apiKeys) : IRequestHandler<DeleteApiKey>
{
    public async Task HandleAsync(DeleteApiKey request, CancellationToken cancellationToken = default)
    {
        ApiKey? apiKey = await apiKeys.DeleteAsync(request.ApiKeyId, cancellationToken);
        if (apiKey is null)
            throw new EntityNotFoundException($"Could not find the ApiKey '{request.ApiKeyId}'.");
    }
}

public partial class ApiKeysController
{
    /// <summary>
    /// Revokes an API key.
    /// </summary>
    /// <remarks>
    /// The API key is deleted and can no longer be used to authenticate.
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
