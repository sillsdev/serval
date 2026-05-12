namespace Serval.DataFiles.Features.Corpora;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/corpora")]
[OpenApiTag("Corpora")]
public partial class CorporaController(IAuthorizationService authService) : ServalControllerBase(authService) { }
