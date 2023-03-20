namespace Serval.Webhooks.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/hooks")]
public class WebhooksController : ServalControllerBase
{
    private readonly IWebhookService _hookService;
    private readonly IMapper _mapper;

    public WebhooksController(IAuthorizationService authService, IWebhookService hookService, IMapper mapper)
        : base(authService)
    {
        _hookService = hookService;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all webhooks.
    /// </summary>
    /// <response code="200">The webhooks.</response>
    [Authorize(Scopes.ReadHooks)]
    [HttpGet]
    public async Task<IEnumerable<WebhookDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _hookService.GetAllAsync(User.Identity!.Name!, cancellationToken)).Select(
            _mapper.Map<WebhookDto>
        );
    }

    /// <summary>
    /// Gets a webhook.
    /// </summary>
    /// <param name="id">The webhook id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The webhook.</response>
    /// <response code="403">The authenticated client does not own the webhook.</response>
    [Authorize(Scopes.ReadHooks)]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WebhookDto>> GetAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        Webhook? hook = await _hookService.GetAsync(id, cancellationToken);
        if (hook == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(hook))
            return Forbid();

        return Ok(_mapper.Map<WebhookDto>(hook));
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
        var newHook = new Webhook
        {
            Url = hookConfig.PayloadUrl,
            Secret = hookConfig.Secret,
            Events = hookConfig.Events.ToList(),
            Owner = User.Identity!.Name!
        };

        await _hookService.CreateAsync(newHook, cancellationToken);
        WebhookDto dto = _mapper.Map<WebhookDto>(newHook);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Deletes a webhook.
    /// </summary>
    /// <param name="id">The webhook id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The webhook was successfully deleted.</response>
    /// <response code="403">The authenticated client does not own the webhook.</response>
    [Authorize(Scopes.DeleteHooks)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
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
}
