namespace Serval.Webhooks.Features.Webhooks;

public record GetWebhook(string Owner, string WebhookId) : IRequest<GetWebhookResponse>;

public record GetWebhookResponse(WebhookDto Webhook);

public class GetWebhookHandler(IRepository<Webhook> webhooks, DtoMapper mapper)
    : IRequestHandler<GetWebhook, GetWebhookResponse>
{
    public async Task<GetWebhookResponse> HandleAsync(GetWebhook request, CancellationToken cancellationToken)
    {
        Webhook webhook = await webhooks.CheckOwnerAsync(request.WebhookId, request.Owner, cancellationToken);
        return new(mapper.Map(webhook));
    }
}

public partial class WebhooksController
{
    /// <summary>
    /// Gets a webhook.
    /// </summary>
    /// <param name="id">The webhook id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The webhook.</response>
    /// <response code="403">The authenticated client does not own the webhook.</response>
    /// <response code="404">The webhook does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadHooks)]
    [HttpGet("{id}", Name = Endpoints.GetWebhook)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WebhookDto>> GetAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<GetWebhook, GetWebhookResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetWebhookResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok(response.Webhook);
    }
}
