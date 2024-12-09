namespace Serval.WordAlignment.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/word-alignment/engine-types")]
[OpenApiTag("Word Alignment Engines")]
public class WordAlignmentEngineTypesController(IAuthorizationService authService, IEngineService engineService)
    : ServalControllerBase(authService)
{
    private readonly IEngineService _engineService = engineService;

    /// <summary>
    /// Get queue information for a given engine type
    /// </summary>
    /// <param name="engineType">A valid engine type: statistical or echo-word-alignment</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Queue information for the specified engine type</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{engineType}/queues")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<QueueDto>> GetQueueAsync(
        [NotNull] string engineType,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Map(
                await _engineService.GetQueueAsync(engineType.ToPascalCase(), cancellationToken: cancellationToken)
            );
        }
        catch (InvalidOperationException ioe)
        {
            return BadRequest(ioe.Message);
        }
    }

    private static QueueDto Map(Queue source) =>
        new() { Size = source.Size, EngineType = source.EngineType.ToKebabCase() };
}
