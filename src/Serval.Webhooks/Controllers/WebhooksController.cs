namespace Serval.Webhooks.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/hooks")]
public class WebhooksController : ServalControllerBase
{
    private readonly IWebhookService _hookService;
    private readonly IUrlService _urlService;

    public WebhooksController(IAuthorizationService authService, IWebhookService hookService, IUrlService urlService)
        : base(authService)
    {
        _hookService = hookService;
        _urlService = urlService;
    }

    /// <summary>
    /// Gets all webhooks.
    /// </summary>
    /// <response code="200">The webhooks.</response>
    [Authorize(Scopes.ReadHooks)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IEnumerable<WebhookDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _hookService.GetAllAsync(Owner, cancellationToken)).Select(Map);
    }

    /// <summary>
    /// Gets a webhook.
    /// </summary>
    /// <param name="id">The webhook id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The webhook.</response>
    /// <response code="403">The authenticated client does not own the webhook.</response>
    /// <response code="404">The webhook does not exist</response>
    [Authorize(Scopes.ReadHooks)]
    [HttpGet("{id}", Name = "GetWebhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookDto>> GetAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        Webhook? hook = await _hookService.GetAsync(id, cancellationToken);
        if (hook == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(hook))
            return Forbid();

        return Ok(Map(hook));
    }

    /// <summary>
    /// Creates a new webhook.
    /// </summary>
    /// <param name="hookConfig">The webhook configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="201">The webhook was created successfully.</response>
    [Authorize(Scopes.CreateHooks)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<WebhookDto>> CreateAsync(
        [FromBody] WebhookConfigDto hookConfig,
        CancellationToken cancellationToken
    )
    {
        Webhook hook = Map(hookConfig);
        await _hookService.CreateAsync(hook, cancellationToken);
        WebhookDto dto = Map(hook);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Deletes a webhook.
    /// </summary>
    /// <param name="id">The webhook id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The webhook was successfully deleted.</response>
    /// <response code="403">The authenticated client does not own the webhook.</response>
    /// <response code="404">The webhook does not exist</response>
    [Authorize(Scopes.DeleteHooks)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        Webhook? hook = await _hookService.GetAsync(id, cancellationToken);
        if (hook == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(hook))
            return Forbid();

        if (!await _hookService.DeleteAsync(id, cancellationToken))
            return NotFound();
        return Ok();
    }

    private WebhookDto Map(Webhook source)
    {
        return new WebhookDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl("GetWebhook", new { id = source.Id }),
            PayloadUrl = source.Url,
            Events = source.Events.ToList()
        };
    }

    private Webhook Map(WebhookConfigDto source)
    {
        return new Webhook
        {
            Url = source.PayloadUrl,
            Secret = source.Secret,
            Events = source.Events.ToList(),
            Owner = Owner
        };
    }
}
