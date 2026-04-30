namespace Serval.Translation.Features.Engines;

public record PretranslationConfidenceDto
{
    public required IReadOnlyList<string> TargetRefs { get; init; }
    public required double Confidence { get; init; }
}

public record GetAllPretranslationConfidences(string Owner, string EngineId, string ParallelCorpusId)
    : IRequest<GetAllPretranslationConfidencesResponse>;

public record GetAllPretranslationConfidencesResponse(
    PretranslationStatus Status,
    IReadOnlyList<PretranslationConfidenceDto>? PretranslationConfidences = null
);

public class GetAllPretranslationConfidencesHandler(
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    ILogger<GetAllPretranslationConfidencesHandler> logger
) : IRequestHandler<GetAllPretranslationConfidences, GetAllPretranslationConfidencesResponse>
{
    public async Task<GetAllPretranslationConfidencesResponse> HandleAsync(
        GetAllPretranslationConfidences request,
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
                && pt.CorpusRef == request.ParallelCorpusId,
            cancellationToken
        );
        logger.LogInformation(
            "Returning {Count} pretranslation confidences for engine {EngineId}, and parallel corpus {ParallelCorpusId}",
            results.Count,
            request.EngineId,
            request.ParallelCorpusId
        );
        return new(status, PretranslationConfidences: [.. results.Select(Map)]);
    }

    private static PretranslationConfidenceDto Map(Pretranslation source) =>
        new() { TargetRefs = source.TargetRefs ?? [], Confidence = source.Confidence ?? -1.0 };
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get all pretranslation confidences in a parallel corpus of a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <response code="200">The confidence values.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/parallel-corpora/{parallelCorpusId}/confidences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<PretranslationConfidenceDto>>> GetAllPretranslationConfidencesAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        [FromServices]
            IRequestHandler<GetAllPretranslationConfidences, GetAllPretranslationConfidencesResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllPretranslationConfidencesResponse response = await handler.HandleAsync(
            new(Owner, id, parallelCorpusId),
            cancellationToken
        );
        if (response.Status == PretranslationStatus.CorpusNotFound)
            return NotFound();
        if (response.Status == PretranslationStatus.NotBuilt)
            return Conflict();
        return Ok(response.PretranslationConfidences);
    }
}
