namespace Serval.WordAlignment.Features.Engines;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/word-alignment/engines")]
[OpenApiTag("Word Alignment Engines")]
public partial class WordAlignmentEnginesController(IAuthorizationService authService)
    : ServalControllerBase(authService) { }
