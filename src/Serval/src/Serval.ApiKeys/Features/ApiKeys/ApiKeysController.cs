namespace Serval.ApiKeys.Features.ApiKeys;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/api-keys")]
public partial class ApiKeysController(IAuthorizationService authService) : ServalControllerBase(authService) { }
