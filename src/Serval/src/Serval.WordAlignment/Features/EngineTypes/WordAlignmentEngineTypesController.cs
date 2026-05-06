namespace Serval.WordAlignment.Features.EngineTypes;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/word-alignment/engine-types")]
[OpenApiTag("Word Alignment Engines")]
public partial class WordAlignmentEngineTypesController(IAuthorizationService authService)
    : ServalControllerBase(authService);
