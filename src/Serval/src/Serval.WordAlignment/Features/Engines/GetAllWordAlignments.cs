namespace Serval.WordAlignment.Features.Engines;

public record GetAllWordAlignments(string Owner, string EngineId, string ParallelCorpusId, string? TextId)
    : IRequest<GetAllWordAlignmentsResponse>;

public record GetAllWordAlignmentsResponse(
    WordAlignmentStatus Status,
    IReadOnlyList<WordAlignmentDto>? WordAlignments = null
);

public class GetAllWordAlignmentsHandler(
    IRepository<Engine> engines,
    IRepository<Models.WordAlignment> wordAlignments,
    ILogger<GetAllWordAlignmentsHandler> logger
) : IRequestHandler<GetAllWordAlignments, GetAllWordAlignmentsResponse>
{
    public async Task<GetAllWordAlignmentsResponse> HandleAsync(
        GetAllWordAlignments request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);

        WordAlignmentStatus status = engine.GetParallelCorpusWordAlignmentStatus(request.ParallelCorpusId);
        if (status != WordAlignmentStatus.Found)
            return new(status);

        IReadOnlyList<Models.WordAlignment> results = await wordAlignments.GetAllAsync(
            pt =>
                pt.EngineRef == request.EngineId
                && pt.ModelRevision == engine.ModelRevision
                && pt.CorpusRef == request.ParallelCorpusId
                && (request.TextId == null || pt.TextId == request.TextId),
            cancellationToken
        );
        logger.LogInformation(
            "Returning {Count} word alignments for engine {EngineId}, parallel corpus {ParallelCorpusId}, and query {TextId}",
            results.Count,
            request.EngineId,
            request.ParallelCorpusId,
            request.TextId
        );
        return new(status, WordAlignments: [.. results.Select(DtoMapper.Map)]);
    }
}

public partial class WordAlignmentEnginesController
{
    /// <summary>
    /// Get all word alignments in a corpus of a engine
    /// </summary>
    /// <remarks>
    /// Word alignments are arranged in a list of dictionaries with the following fields per word alignment:
    /// * **`textId`**: The text id of the source file defined when the corpus was created.
    /// * **`refs`** (a list of strings): A list of references including:
    ///   * The references defined in the source file per line, if any.
    ///   * An auto-generated reference of `[textId]:[lineNumber]`, 1 indexed.
    /// * **`sourceTokens`**: the tokenized source segment
    /// * **`targetTokens`**: the tokenized target segment
    /// * **`alignment`**: a list of aligned word pairs with associated scores
    ///
    /// Word alignments can be filtered by text id if provided.
    /// Only word alignments for the most recent successful build of the engine are returned.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="textId">The text id (optional)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The word alignments</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}/corpora/{corpusId}/word-alignments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<WordAlignmentDto>>> GetAllWordAlignmentsAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromQuery(Name = "text-id")] string? textId,
        [FromServices] IRequestHandler<GetAllWordAlignments, GetAllWordAlignmentsResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllWordAlignmentsResponse response = await handler.HandleAsync(
            new(Owner, id, corpusId, textId),
            cancellationToken
        );
        if (response.Status == WordAlignmentStatus.CorpusNotFound)
            return NotFound();
        if (response.Status == WordAlignmentStatus.NotBuilt)
            return Conflict();
        return Ok(response.WordAlignments);
    }
}
