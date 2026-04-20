namespace Serval.Translation.Features.Engines;

public record WordGraphDto
{
    public required IReadOnlyList<string> SourceTokens { get; init; }
    public required float InitialStateScore { get; init; }
    public required IReadOnlySet<int> FinalStates { get; init; }
    public required IReadOnlyList<WordGraphArcDto> Arcs { get; init; }
}

public record WordGraphArcDto
{
    public required int PrevState { get; init; }
    public required int NextState { get; init; }
    public required double Score { get; init; }
    public required IReadOnlyList<string> TargetTokens { get; init; }
    public required IReadOnlyList<double> Confidences { get; init; }
    public required int SourceSegmentStart { get; init; }
    public required int SourceSegmentEnd { get; init; }
    public required IReadOnlyList<AlignedWordPairDto> Alignment { get; init; }
    public required IReadOnlyList<IReadOnlySet<TranslationSource>> Sources { get; init; }
}

public record GetWordGraph(string Owner, string EngineId, string Segment) : IRequest<GetWordGraphResponse>;

public record GetWordGraphResponse(
    [property: MemberNotNullWhen(true, nameof(GetWordGraphResponse.WordGraph))] bool IsAvailable,
    WordGraphDto? WordGraph = null
);

public class GetWordGraphHandler(IRepository<Engine> engines, IEngineServiceFactory engineServiceFactory)
    : IRequestHandler<GetWordGraph, GetWordGraphResponse>
{
    public async Task<GetWordGraphResponse> HandleAsync(
        GetWordGraph request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);
        if (engine.ModelRevision == 0)
            return new(IsAvailable: false);

        WordGraphContract wordGraph = await engineServiceFactory
            .GetEngineService(engine.Type)
            .GetWordGraphAsync(request.EngineId, request.Segment, cancellationToken);

        return new(
            IsAvailable: true,
            new WordGraphDto
            {
                SourceTokens = wordGraph.SourceTokens,
                InitialStateScore = (float)wordGraph.InitialStateScore,
                FinalStates = wordGraph.FinalStates,
                Arcs = wordGraph
                    .Arcs.Select(a => new WordGraphArcDto
                    {
                        PrevState = a.PrevState,
                        NextState = a.NextState,
                        Score = Math.Round(a.Score, 8),
                        TargetTokens = a.TargetTokens,
                        Confidences = a.Confidences.Select(c => Math.Round(c, 8)).ToList(),
                        SourceSegmentStart = a.SourceSegmentStart,
                        SourceSegmentEnd = a.SourceSegmentEnd,
                        Alignment = a
                            .Alignment.Select(wp => new AlignedWordPairDto
                            {
                                SourceIndex = wp.SourceIndex,
                                TargetIndex = wp.TargetIndex,
                            })
                            .ToList(),
                        Sources = a.Sources,
                    })
                    .ToList(),
            }
        );
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get the word graph that represents all possible translations of a segment of text
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="segment">The source segment</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The word graph result</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The method is not supported.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpPost("{id}/get-word-graph")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordGraphDto>> GetWordGraphAsync(
        [NotNull] string id,
        [FromBody] string segment,
        [FromServices] IRequestHandler<GetWordGraph, GetWordGraphResponse> handler,
        [FromServices] ILogger<TranslationEnginesController> logger,
        CancellationToken cancellationToken
    )
    {
        GetWordGraphResponse response = await handler.HandleAsync(new(Owner, id, segment), cancellationToken);
        if (!response.IsAvailable)
            return Conflict();
        logger.LogInformation("Got word graph for engine {EngineId}", id);
        return Ok(response.WordGraph);
    }
}
