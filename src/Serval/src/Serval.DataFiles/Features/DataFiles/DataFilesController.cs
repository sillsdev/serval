namespace Serval.DataFiles.Features.DataFiles;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/files")]
[OpenApiTag("Files")]
public partial class DataFilesController(IAuthorizationService authService) : ServalControllerBase(authService) { }
