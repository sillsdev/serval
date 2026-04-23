namespace Serval.Webhooks.Features.Webhooks;

public record WebhookConfigDto
{
    /// <summary>
    /// The payload URL.
    /// </summary>
    public required string PayloadUrl { get; init; }

    /// <summary>
    /// The shared secret.
    /// </summary>
    public required string Secret { get; init; }

    /// <summary>
    /// The webhook events.
    /// </summary>
    public required IList<WebhookEvent> Events { get; init; }
}

public record CreateWebhook(string Owner, WebhookConfigDto WebhookConfig) : IRequest<CreateWebhookResponse>;

public record CreateWebhookResponse(WebhookDto Webhook);

public class CreateWebhookHandler(IDataAccessContext dataAccessContext, DtoMapper mapper, IRepository<Webhook> webhooks)
    : IRequestHandler<CreateWebhook, CreateWebhookResponse>
{
    public async Task<CreateWebhookResponse> HandleAsync(
        CreateWebhook request,
        CancellationToken cancellationToken = default
    ) =>
        await dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Webhook webhook = new()
                {
                    Events = [.. request.WebhookConfig.Events],
                    Owner = request.Owner,
                    Secret = request.WebhookConfig.Secret,
                    Url = request.WebhookConfig.PayloadUrl,
                };
                await webhooks.InsertAsync(webhook, ct);
                return new CreateWebhookResponse(mapper.Map(webhook));
            },
            cancellationToken
        );
}

public partial class WebhooksController
{
    /// <summary>
    /// Creates a new webhook.
    /// </summary>
    /// <param name="webhookConfig">The webhook configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="201">The webhook was created successfully.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.CreateHooks)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WebhookDto>> CreateAsync(
        [FromBody] WebhookConfigDto webhookConfig,
        [FromServices] IRequestHandler<CreateWebhook, CreateWebhookResponse> handler,
        CancellationToken cancellationToken
    )
    {
        CreateWebhookResponse response = await handler.HandleAsync(new(Owner, webhookConfig), cancellationToken);
        return Created(response.Webhook.Url, response.Webhook);
    }
}
