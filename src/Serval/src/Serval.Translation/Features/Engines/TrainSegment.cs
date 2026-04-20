namespace Serval.Translation.Features.Engines;

public record SegmentPairDto
{
    public required string SourceSegment { get; init; }
    public required string TargetSegment { get; init; }
    public required bool SentenceStart { get; init; }
}

public record TrainSegment(string Owner, string EngineId, SegmentPairDto SegmentPair) : IRequest<TrainSegmentResponse>;

public record TrainSegmentResponse(bool IsAvailable);

public class TrainSegmentHandler(IRepository<Engine> engines, IEngineServiceFactory engineServiceFactory)
    : IRequestHandler<TrainSegment, TrainSegmentResponse>
{
    public async Task<TrainSegmentResponse> HandleAsync(
        TrainSegment request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);
        if (engine.ModelRevision == 0)
            return new(IsAvailable: false);

        await engineServiceFactory
            .GetEngineService(engine.Type)
            .TrainSegmentPairAsync(
                engine.Id,
                request.SegmentPair.SourceSegment,
                request.SegmentPair.TargetSegment,
                request.SegmentPair.SentenceStart,
                cancellationToken
            );
        return new(IsAvailable: true);
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Incrementally train a translation engine with a segment pair
    /// </summary>
    /// <remarks>
    /// A segment pair consists of a source and target segment as well as a boolean flag `sentenceStart`
    /// that should be set to `true` if this segment pair forms the beginning of a sentence. (This information
    /// will be used to reconstruct proper capitalization when training/inferencing).
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="segmentPair">The segment pair</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engine was trained successfully.</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The method is not supported.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/train-segment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> TrainSegmentAsync(
        [NotNull] string id,
        [FromBody] SegmentPairDto segmentPair,
        [FromServices] IRequestHandler<TrainSegment, TrainSegmentResponse> handler,
        [FromServices] ILogger<TranslationEnginesController> logger,
        CancellationToken cancellationToken
    )
    {
        TrainSegmentResponse response = await handler.HandleAsync(new(Owner, id, segmentPair), cancellationToken);
        if (!response.IsAvailable)
            return Conflict();
        logger.LogInformation("Trained segment pair for engine {EngineId}", id);
        return Ok();
    }
}
