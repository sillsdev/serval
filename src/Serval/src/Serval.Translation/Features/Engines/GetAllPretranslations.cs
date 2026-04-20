namespace Serval.Translation.Features.Engines;

public record GetAllPretranslations(string Owner, string EngineId, string ParallelCorpusId, string? TextId)
    : IRequest<GetAllPretranslationsResponse>;

public record GetAllPretranslationsResponse(
    PretranslationStatus Status,
    IReadOnlyList<PretranslationDto>? Pretranslations = null
);

public class GetAllPretranslationsHandler(
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    ILogger<GetAllPretranslationsHandler> logger
) : IRequestHandler<GetAllPretranslations, GetAllPretranslationsResponse>
{
    public async Task<GetAllPretranslationsResponse> HandleAsync(
        GetAllPretranslations request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);

        PretranslationStatus status = engine.GetParallelCorpusPretranslationStatus(request.ParallelCorpusId);
        if (status != PretranslationStatus.Found)
            return new(status);

        IReadOnlyList<Pretranslation> results = await pretranslations.GetAllAsync(
            pt =>
                pt.EngineRef == request.EngineId
                && pt.ModelRevision == engine.ModelRevision
                && pt.CorpusRef == request.ParallelCorpusId
                && (request.TextId == null || pt.TextId == request.TextId),
            cancellationToken
        );
        logger.LogInformation(
            "Returning {Count} pretranslations for engine {EngineId}, parallel corpus {ParallelCorpusId}, and query {TextId}",
            results.Count,
            request.EngineId,
            request.ParallelCorpusId,
            request.TextId
        );
        return new(status, Pretranslations: [.. results.Select(DtoMapper.Map)]);
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get all pretranslations in a parallel corpus of a translation engine
    /// </summary>
    /// <remarks>
    /// Pretranslations are arranged in a list of dictionaries with the following fields per pretranslation:
    /// * **`textId`**: The text id of the source file defined when the corpus was created.
    /// * **`refs`** (a list of strings): A list of references including:
    ///   * The references defined in the source file per line, if any.
    ///   * An auto-generated reference of `[textId]:[lineNumber]`, 1 indexed.
    /// * **`translation`**: the text of the pretranslation
    ///
    /// Pretranslations can be filtered by text id if provided.
    /// Only pretranslations for the most recent successful build of the engine are returned.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="textId">The text id (optional)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The pretranslations</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/parallel-corpora/{parallelCorpusId}/pretranslations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<PretranslationDto>>> GetAllPretranslationsAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        [FromQuery(Name = "text-id")] string? textId,
        [FromServices] IRequestHandler<GetAllPretranslations, GetAllPretranslationsResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllPretranslationsResponse response = await handler.HandleAsync(
            new(Owner, id, parallelCorpusId, textId),
            cancellationToken
        );
        if (response.Status == PretranslationStatus.CorpusNotFound)
            return NotFound();
        if (response.Status == PretranslationStatus.NotBuilt)
            return Conflict();
        return Ok(response.Pretranslations);
    }
}
