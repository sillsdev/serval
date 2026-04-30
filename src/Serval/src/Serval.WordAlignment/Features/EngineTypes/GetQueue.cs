namespace Serval.WordAlignment.Features.EngineTypes;

public record GetQueue(string EngineType) : IRequest<GetQueueResponse>;

public record GetQueueResponse(QueueDto? Queue = null);

public class GetQueueHandler(IEngineServiceFactory engineServiceFactory) : IRequestHandler<GetQueue, GetQueueResponse>
{
    public async Task<GetQueueResponse> HandleAsync(GetQueue request, CancellationToken cancellationToken)
    {
        if (
            engineServiceFactory.TryGetEngineService(
                request.EngineType.ToPascalCase(),
                out IWordAlignmentEngineService? engineService
            )
        )
        {
            int size = await engineService.GetQueueSizeAsync(cancellationToken);
            return new(new QueueDto { EngineType = request.EngineType.ToKebabCase(), Size = size });
        }
        return new();
    }
}

public partial class WordAlignmentEngineTypesController
{
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
        [FromServices] IRequestHandler<GetQueue, GetQueueResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetQueueResponse response = await handler.HandleAsync(new(engineType), cancellationToken);
        if (response.Queue is not null)
            return Ok(response.Queue);
        return NotFound();
    }
}
