namespace Serval.WordAlignment.Features.Engines;

public record WordAlignmentRequestDto
{
    public required string SourceSegment { get; init; }
    public required string TargetSegment { get; init; }
}

public record WordAlignmentResultDto
{
    public required IReadOnlyList<string> SourceTokens { get; init; }
    public required IReadOnlyList<string> TargetTokens { get; init; }
    public required IReadOnlyList<AlignedWordPairDto> Alignment { get; init; }
}

public record Align(string Owner, string EngineId, string SourceSegment, string TargetSegment)
    : IRequest<WordAlignmentResponse>;

public record WordAlignmentResponse
{
    [property: MemberNotNullWhen(true, nameof(Result))]
    public bool IsAvailable { get; init; }
    public WordAlignmentResultDto? Result { get; init; }
}

public class AlignHandler(IRepository<Engine> engines, IEngineServiceFactory engineServiceFactory)
    : IRequestHandler<Align, WordAlignmentResponse>
{
    public async Task<WordAlignmentResponse> HandleAsync(Align request, CancellationToken cancellationToken = default)
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);
        if (engine.ModelRevision == 0)
            return new WordAlignmentResponse { IsAvailable = false };

        WordAlignmentResultContract result = await engineServiceFactory
            .GetEngineService(engine.Type)
            .AlignAsync(request.EngineId, request.SourceSegment, request.TargetSegment, cancellationToken);

        return new WordAlignmentResponse
        {
            IsAvailable = true,
            Result = new WordAlignmentResultDto
            {
                SourceTokens = result.SourceTokens,
                TargetTokens = result.TargetTokens,
                Alignment =
                [
                    .. result.Alignment.Select(p => new AlignedWordPairDto
                    {
                        SourceIndex = p.SourceIndex,
                        TargetIndex = p.TargetIndex,
                        Score = p.Score,
                    }),
                ],
            },
        };
    }
}

public partial class WordAlignmentEnginesController
{
    /// <summary>
    /// Align words between a source and target segment
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="wordAlignmentRequest">The source and target segment</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The word alignment result</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The method is not supported.</response>
    /// <response code="409">The engine needs to be built before it can align segments.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpPost("{id}/align")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentResultDto>> AlignAsync(
        [NotNull] string id,
        [FromBody] WordAlignmentRequestDto wordAlignmentRequest,
        [FromServices] IRequestHandler<Align, WordAlignmentResponse> handler,
        [FromServices] ILogger<WordAlignmentEnginesController> logger,
        CancellationToken cancellationToken
    )
    {
        WordAlignmentResponse response = await handler.HandleAsync(
            new Align(Owner, id, wordAlignmentRequest.SourceSegment, wordAlignmentRequest.TargetSegment),
            cancellationToken
        );
        if (!response.IsAvailable)
            return Conflict();

        logger.LogInformation("Got word alignment for engine {EngineId}", id);
        return Ok(response.Result);
    }
}
