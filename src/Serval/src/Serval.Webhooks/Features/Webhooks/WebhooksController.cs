namespace Serval.Webhooks.Features.Webhooks;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/hooks")]
public partial class WebhooksController(IAuthorizationService authService) : ServalControllerBase(authService) { }
