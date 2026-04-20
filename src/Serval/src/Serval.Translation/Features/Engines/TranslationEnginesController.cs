namespace Serval.Translation.Features.Engines;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/engines")]
[OpenApiTag("Translation Engines")]
public partial class TranslationEnginesController(IAuthorizationService authService)
    : ServalControllerBase(authService) { }
