namespace Serval.Translation.Features.EngineTypes;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/engine-types")]
[OpenApiTag("Translation Engines")]
public partial class TranslationEngineTypesController(IAuthorizationService authService)
    : ServalControllerBase(authService);
