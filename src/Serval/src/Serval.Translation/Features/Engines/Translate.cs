namespace Serval.Translation.Features.Engines;

public record TranslationResultDto
{
    public required string Translation { get; init; }
    public required IReadOnlyList<string> SourceTokens { get; init; }
    public required IReadOnlyList<string> TargetTokens { get; init; }
    public required IReadOnlyList<double> Confidences { get; init; }
    public required IReadOnlyList<IReadOnlySet<TranslationSource>> Sources { get; init; }
    public required IReadOnlyList<AlignedWordPairDto> Alignment { get; init; }
    public required IReadOnlyList<PhraseDto> Phrases { get; init; }
}

public record PhraseDto
{
    public required int SourceSegmentStart { get; init; }
    public required int SourceSegmentEnd { get; init; }
    public required int TargetSegmentCut { get; init; }
}

public record Translate(string Owner, string EngineId, string Segment, int N = 1) : IRequest<TranslateResponse>;

public record TranslateResponse(
    [property: MemberNotNullWhen(true, nameof(Results))] bool IsAvailable,
    IEnumerable<TranslationResultDto>? Results = null
);

public class TranslateHandler(IRepository<Engine> engines, IEngineServiceFactory engineServiceFactory)
    : IRequestHandler<Translate, TranslateResponse>
{
    public async Task<TranslateResponse> HandleAsync(Translate request, CancellationToken cancellationToken)
    {
        Engine? engine = await engines.GetAsync(request.EngineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");
        if (engine.Owner != request.Owner)
            throw new ForbiddenException();
        if (engine.ModelRevision == 0)
            return new(IsAvailable: false);

        IReadOnlyList<TranslationResultContract> results = await engineServiceFactory
            .GetEngineService(engine.Type)
            .TranslateAsync(request.EngineId, request.N, request.Segment, cancellationToken);

        return new(
            IsAvailable: true,
            results.Select(r => new TranslationResultDto
            {
                Translation = r.Translation,
                SourceTokens = r.SourceTokens,
                TargetTokens = r.TargetTokens,
                Confidences = r.Confidences.Select(c => Math.Round(c, 8)).ToList(),
                Sources = r.Sources,
                Alignment = r
                    .Alignment.Select(wp => new AlignedWordPairDto
                    {
                        SourceIndex = wp.SourceIndex,
                        TargetIndex = wp.TargetIndex,
                    })
                    .ToList(),
                Phrases = r
                    .Phrases.Select(p => new PhraseDto
                    {
                        SourceSegmentStart = p.SourceSegmentStart,
                        SourceSegmentEnd = p.SourceSegmentEnd,
                        TargetSegmentCut = p.TargetSegmentCut,
                    })
                    .ToList(),
            })
        );
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Translate a segment of text
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="segment">The source segment</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The translation result</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The method is not supported.</response>
    /// <response code="409">The engine needs to be built before it can translate segments.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpPost("{id}/translate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationResultDto>> TranslateAsync(
        [NotNull] string id,
        [FromBody] string segment,
        [FromServices] IRequestHandler<Translate, TranslateResponse> handler,
        CancellationToken cancellationToken
    )
    {
        TranslateResponse response = await handler.HandleAsync(new Translate(Owner, id, segment), cancellationToken);
        if (!response.IsAvailable)
            return Conflict();
        _logger.LogInformation("Translated segment for engine {EngineId}", id);
        return Ok(response.Results?.First());
    }

    /// <summary>
    /// Returns the top N translations of a segment
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="n">The number of translations to generate</param>
    /// <param name="segment">The source segment</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The translation results</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The method is not supported.</response>
    /// <response code="409">The engine needs to be built before it can translate segments.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpPost("{id}/translate/{n}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<TranslationResultDto>>> TranslateNAsync(
        [NotNull] string id,
        [NotNull] int n,
        [FromBody] string segment,
        [FromServices] IRequestHandler<Translate, TranslateResponse> handler,
        CancellationToken cancellationToken
    )
    {
        TranslateResponse response = await handler.HandleAsync(new(Owner, id, segment, n), cancellationToken);
        if (!response.IsAvailable)
            return Conflict();
        _logger.LogInformation("Translated {n} segments for engine {EngineId}", n, id);
        return Ok(response.Results);
    }
}
