namespace Serval.Webhooks.Features.Webhooks;

public record DeleteWebhook(string Owner, string WebhookId) : IRequest;

public class DeleteWebhookHandler(IRepository<Webhook> webhooks) : IRequestHandler<DeleteWebhook>
{
    public async Task HandleAsync(DeleteWebhook request, CancellationToken cancellationToken = default)
    {
        await webhooks.CheckOwnerAsync(request.WebhookId, request.Owner, cancellationToken);
        Webhook? webhook = await webhooks.DeleteAsync(request.WebhookId, cancellationToken);
        if (webhook is null)
            throw new EntityNotFoundException($"Could not find the Webhook '{request.WebhookId}'.");
    }
}

public partial class WebhooksController
{
    /// <summary>
    /// Deletes a webhook.
    /// </summary>
    /// <param name="id">The webhook id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The webhook was successfully deleted.</response>
    /// <response code="403">The authenticated client does not own the webhook.</response>
    /// <response code="404">The webhook does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.DeleteHooks)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<DeleteWebhook> handler,
        CancellationToken cancellationToken
    )
    {
        await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok();
    }
}
