namespace Serval.Translation.Features.Builds;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/builds")]
[OpenApiTag("Translation Engines")]
public partial class TranslationBuildsController(IAuthorizationService authService)
    : ServalControllerBase(authService) { }
