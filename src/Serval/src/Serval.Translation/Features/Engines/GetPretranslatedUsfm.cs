namespace Serval.Translation.Features.Engines;

public record GetPretranslatedUsfm(
    string Owner,
    string EngineId,
    string ParallelCorpusId,
    string TextId,
    PretranslationUsfmTextOrigin TextOrigin,
    PretranslationUsfmTemplate Template,
    PretranslationUsfmMarkerBehavior ParagraphMarkerBehavior,
    PretranslationUsfmMarkerBehavior EmbedBehavior,
    PretranslationUsfmMarkerBehavior StyleMarkerBehavior,
    PretranslationNormalizationBehavior QuoteNormalizationBehavior
) : IRequest<GetPretranslatedUsfmResponse>;

public record GetPretranslatedUsfmResponse(PretranslationStatus Status, string? Usfm = null);

public class GetPretranslatedUsfmHandler(
    IRepository<Engine> engines,
    IUsfmGenerationService pretranslationService,
    ILogger<GetPretranslatedUsfmHandler> logger
) : IRequestHandler<GetPretranslatedUsfm, GetPretranslatedUsfmResponse>
{
    public async Task<GetPretranslatedUsfmResponse> HandleAsync(
        GetPretranslatedUsfm request,
        CancellationToken cancellationToken = default
    )
    {
        Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);

        PretranslationStatus status = engine.GetParallelCorpusPretranslationStatus(request.ParallelCorpusId);
        if (status != PretranslationStatus.Found)
            return new(status);

        string usfm = await pretranslationService.GetUsfmAsync(
            request.EngineId,
            engine.ModelRevision,
            request.ParallelCorpusId,
            request.TextId,
            request.TextOrigin,
            request.Template,
            request.ParagraphMarkerBehavior,
            request.EmbedBehavior,
            request.StyleMarkerBehavior,
            request.QuoteNormalizationBehavior,
            cancellationToken
        );
        if (usfm != "")
        {
            logger.LogInformation(
                "Returning USFM for {TextId} in engine {EngineId} for parallel corpus {ParallelCorpusId}",
                request.TextId,
                request.EngineId,
                request.ParallelCorpusId
            );
        }
        return new(status, Usfm: usfm);
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get a pretranslated Scripture book in USFM format.
    /// </summary>
    /// <remarks>
    /// The text that populates the USFM structure can be controlled by the `text-origin` parameter:
    /// * `PreferExisting`: The existing and pretranslated texts are merged into the USFM, preferring existing text. **This is the default**.
    /// * `PreferPretranslated`: The existing and pretranslated texts are merged into the USFM, preferring pretranslated text.
    /// * `OnlyExisting`: Return the existing target USFM file with no modifications (except updating the USFM id if needed).
    /// * `OnlyPretranslated`: Only the pretranslated text is returned; all existing text in the target USFM is removed.
    ///
    /// The source or target book can be used as the USFM template for the pretranslated text. The template can be controlled by the `template` parameter:
    /// * `Auto`: The target book is used as the template if it exists; otherwise, the source book is used. **This is the default**.
    /// * `Source`: The source book is used as the template.
    /// * `Target`: The target book is used as the template.
    ///
    /// The intra-segment USFM markers are handled in the following way:
    /// * Each verse and non-verse text segment is stripped of all intra-segment USFM.
    /// * Reference (\r) and remark (\rem) markers are not translated but carried through from the source to the target.
    ///
    /// Preserving or stripping different types of USFM markers can be controlled by the `paragraph-marker-behavior`, `embed-behavior`, and `style-marker-behavior` parameters.
    /// * `PushToEnd`: The USFM markers (or the entire embed) are preserved and placed at the end of the verse. **This is the default for paragraph markers and embeds**.
    /// * `TryToPlace`: The USFM markers (or the entire embed) are placed in approximately the right location within the verse. **This option is only available for paragraph markers. Quality of placement may differ from language to language.**.
    /// * `Strip`: The USFM markers (or the entire embed) are removed. **This is the default for style markers**.
    ///
    /// Quote normalization behavior is controlled by the `quote-normalization-behavior` parameter options:
    /// * `Normalized`: The quotes in the pretranslated USFM are normalized quotes (typically straight quotes: ', ") in the style of the source data. **This is the default**.
    /// * `Denormalized`: The quotes in the pretranslated USFM are denormalized into the style of the target data. Quote denormalization may not be successful in all contexts. A remark will be added to the USFM listing the chapters that were successfully denormalized.
    ///
    /// Only pretranslations for the most recent successful build of the engine are returned.
    /// The USFM parsing and marker types used are defined here: [this wiki](https://github.com/sillsdev/serval/wiki/USFM-Parsing-and-Translation).
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="textId">The text id</param>
    /// <param name="textOrigin">The source[s] of the data to populate the USFM file with.</param>
    /// <param name="template">The source or target book to use as the USFM template.</param>
    /// <param name="paragraphMarkerBehavior">The behavior of paragraph markers.</param>
    /// <param name="embedBehavior">The behavior of embed markers.</param>
    /// <param name="styleMarkerBehavior">The behavior of style markers.</param>
    /// <param name="quoteNormalizationBehavior">The normalization behavior of quotes.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The book in USFM format</response>
    /// <response code="204">The specified book does not exist in the source or target corpus.</response>
    /// <response code="400">The parallel corpus does not contain a valid Scripture corpus, or the USFM is invalid.</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/parallel-corpora/{parallelCorpusId}/pretranslations/{textId}/usfm")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPretranslatedUsfmAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        [NotNull] string textId,
        [FromQuery(Name = "text-origin")] PretranslationUsfmTextOrigin? textOrigin,
        [FromQuery] PretranslationUsfmTemplate? template,
        [FromQuery(Name = "paragraph-marker-behavior")] PretranslationUsfmMarkerBehavior? paragraphMarkerBehavior,
        [FromQuery(Name = "embed-behavior")] PretranslationUsfmMarkerBehavior? embedBehavior,
        [FromQuery(Name = "style-marker-behavior")] PretranslationUsfmMarkerBehavior? styleMarkerBehavior,
        [FromQuery(Name = "quotation-mark-behavior")] PretranslationNormalizationBehavior? quoteNormalizationBehavior,
        [FromServices] IRequestHandler<GetPretranslatedUsfm, GetPretranslatedUsfmResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetPretranslatedUsfmResponse response = await handler.HandleAsync(
            new(
                Owner,
                id,
                parallelCorpusId,
                textId,
                textOrigin ?? PretranslationUsfmTextOrigin.PreferExisting,
                template ?? PretranslationUsfmTemplate.Auto,
                paragraphMarkerBehavior ?? PretranslationUsfmMarkerBehavior.Preserve,
                embedBehavior ?? PretranslationUsfmMarkerBehavior.Preserve,
                styleMarkerBehavior ?? PretranslationUsfmMarkerBehavior.Strip,
                quoteNormalizationBehavior ?? PretranslationNormalizationBehavior.Normalized
            ),
            cancellationToken
        );
        if (response.Status == PretranslationStatus.CorpusNotFound)
            return NotFound();
        if (response.Status == PretranslationStatus.NotBuilt)
            return Conflict();
        if (string.IsNullOrEmpty(response.Usfm))
            return NoContent();
        return Content(response.Usfm, "text/plain");
    }
}
