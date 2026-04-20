namespace Serval.Translation.Features.Engines;

#pragma warning disable CS0612 // Type or member is obsolete

public record GetCorpusPretranslationsByTextId(string Owner, string EngineId, string CorpusId, string TextId)
    : IRequest<GetCorpusPretranslationsByTextIdResponse>;

public record GetCorpusPretranslationsByTextIdResponse(
    PretranslationStatus Status,
    IReadOnlyList<PretranslationDto>? Pretranslations = null
);

public class GetCorpusPretranslationsByTextIdHandler(
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    ILogger<GetCorpusPretranslationsByTextIdHandler> logger
) : IRequestHandler<GetCorpusPretranslationsByTextId, GetCorpusPretranslationsByTextIdResponse>
{
    public async Task<GetCorpusPretranslationsByTextIdResponse> HandleAsync(
        GetCorpusPretranslationsByTextId request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);

        PretranslationStatus status = engine.GetCorpusPretranslationStatus(request.CorpusId);
        if (status != PretranslationStatus.Found)
            return new(status);

        IReadOnlyList<Pretranslation> results = await pretranslations.GetAllAsync(
            pt =>
                pt.EngineRef == request.EngineId
                && pt.ModelRevision == engine.ModelRevision
                && pt.CorpusRef == request.CorpusId
                && pt.TextId == request.TextId,
            cancellationToken
        );
        logger.LogInformation(
            "Returning {Count} pretranslations for engine {EngineId}, corpus {CorpusId}, and textId {TextId}",
            results.Count,
            request.EngineId,
            request.CorpusId,
            request.TextId
        );
        return new(status, Pretranslations: [.. results.Select(DtoMapper.Map)]);
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get all pretranslations for the specified text in a corpus or parallel corpus of a translation engine
    /// </summary>
    /// <remarks>
    /// Pretranslations are arranged in a list of dictionaries with the following fields per pretranslation:
    /// * **`textId`**: The text id of the source file defined when the corpus was created.
    /// * **`refs`** (a list of strings): A list of references including:
    ///   * The references defined in the source file per line, if any.
    ///   * An auto-generated reference of `[textId]:[lineNumber]`, 1 indexed.
    /// * **`translation`**: the text of the pretranslation
    ///
    /// Only pretranslations for the most recent successful build of the engine are returned.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id or parallel corpus id</param>
    /// <param name="textId">The text id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The pretranslations</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}/pretranslations/{textId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<PretranslationDto>>> GetCorpusPretranslationsByTextIdAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [NotNull] string textId,
        [FromServices]
            IRequestHandler<GetCorpusPretranslationsByTextId, GetCorpusPretranslationsByTextIdResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetCorpusPretranslationsByTextIdResponse response = await handler.HandleAsync(
            new(Owner, id, corpusId, textId),
            cancellationToken
        );
        if (response.Status == PretranslationStatus.CorpusNotFound)
            return NotFound();
        if (response.Status == PretranslationStatus.NotBuilt)
            return Conflict();
        return Ok(response.Pretranslations);
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
