namespace Serval.Webhooks.Features.Webhooks;

public record GetAllWebhooks(string Owner) : IRequest<GetAllWebhooksResponse>;

public record GetAllWebhooksResponse(IEnumerable<WebhookDto> Webhooks);

public class GetAllWebhooksHandler(IRepository<Webhook> webhooks, DtoMapper mapper)
    : IRequestHandler<GetAllWebhooks, GetAllWebhooksResponse>
{
    public async Task<GetAllWebhooksResponse> HandleAsync(GetAllWebhooks request, CancellationToken cancellationToken)
    {
        IEnumerable<WebhookDto> dtos = (
            await webhooks.GetAllAsync(e => e.Owner == request.Owner, cancellationToken)
        ).Select(mapper.Map);
        return new(dtos);
    }
}

public partial class WebhooksController
{
    /// <summary>
    /// Gets all webhooks.
    /// </summary>
    /// <response code="200">The webhooks.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadHooks)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<WebhookDto>> GetAllAsync(
        [FromServices] IRequestHandler<GetAllWebhooks, GetAllWebhooksResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllWebhooksResponse response = await handler.HandleAsync(new(Owner), cancellationToken);
        return response.Webhooks;
    }
}
