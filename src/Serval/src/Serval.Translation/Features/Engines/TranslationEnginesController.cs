namespace Serval.Translation.Features.Engines;

#pragma warning disable CS0612 // Type or member is obsolete

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/engines")]
[OpenApiTag("Translation Engines")]
public partial class TranslationEnginesController(
    IAuthorizationService authService,
    IEngineService engineService,
    IBuildService buildService,
    IPretranslationService pretranslationService,
    IOptionsMonitor<ApiOptions> apiOptions,
    IUrlService urlService,
    ILogger<TranslationEnginesController> logger
) : ServalControllerBase(authService)
{
    private readonly IEngineService _engineService = engineService;
    private readonly IBuildService _buildService = buildService;
    private readonly IPretranslationService _pretranslationService = pretranslationService;
    private readonly IOptionsMonitor<ApiOptions> _apiOptions = apiOptions;
    private readonly IUrlService _urlService = urlService;
    private readonly ILogger<TranslationEnginesController> _logger = logger;

    /// <summary>
    /// Add a corpus to a translation engine (obsolete - use parallel corpora instead)
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **name**: A name to help identify and distinguish the corpus from other corpora
    ///   * The name does not have to be unique since the corpus is uniquely identified by an auto-generated id
    /// * **`sourceLanguage`**: The source language code (See documentation on endpoint /translation/engines/ - "Create a new translation engine" for details on language codes).
    ///   * Normally, this is the same as the engine's `sourceLanguage`.  This may change for future engines as a means of transfer learning.
    /// * **`targetLanguage`**: The target language code (See documentation on endpoint /translation/engines/ - "Create a new translation engine" for details on language codes).
    /// * **`sourceFiles`**: The source files associated with the corpus
    ///   * **`fileId`**: The unique id referencing the uploaded file
    ///   * **`textId`**: The client-defined name to associate source and target files.
    ///     * If the text ids in the source files and target files match, they will be used to train the engine.
    ///     * If selected for pretranslation when building, all source files that have no target file, or lines of text in a source file that have missing or blank lines in the target file will be pretranslated.
    ///     * If a text id is used more than once in source files, the sources will be randomly and evenly mixed for training.
    ///     * For pretranslating, multiple sources with the same text id will be combined, but the first source will always take precedence (no random mixing).
    ///     * For Paratext projects, text id will be ignored - multiple Paratext source projects will always be mixed (as if they have the same text id).
    /// * **`targetFiles`**: The target files associated with the corpus
    ///   * Same as `sourceFiles`, except only a single instance of a text id or a single Paratext project is supported.  There is no mixing or combining of multiple targets.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusConfig">The corpus configuration (see remarks)</param>
    /// <param name="getDataFileHandler"></param>
    /// <param name="idGenerator"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The added corpus</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/corpora")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationCorpusDto>> AddCorpusAsync(
        [NotNull] string id,
        [FromBody] TranslationCorpusConfigDto corpusConfig,
        [FromServices] IRequestHandler<GetDataFile, GetDataFileResponse> getDataFileHandler,
        [FromServices] IIdGenerator idGenerator,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        Corpus corpus = await MapAsync(getDataFileHandler, idGenerator.GenerateId(), corpusConfig, cancellationToken);
        await _engineService.AddCorpusAsync(id, corpus, cancellationToken);
        TranslationCorpusDto dto = Map(id, corpus);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Update a corpus with a new set of files (obsolete - use parallel corpora instead)
    /// </summary>
    /// <remarks>
    /// See posting a new corpus for details of use. Will completely replace corpus' file associations.
    /// Will not affect jobs already queued or running. Will not affect existing pretranslations until new build is complete.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="corpusConfig">The corpus configuration</param>
    /// <param name="getDataFileHandler">The data file handler</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was updated successfully</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPatch("{id}/corpora/{corpusId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationCorpusDto>> UpdateCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromBody] TranslationCorpusUpdateConfigDto corpusConfig,
        [FromServices] IRequestHandler<GetDataFile, GetDataFileResponse> getDataFileHandler,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        Corpus corpus = await _engineService.UpdateCorpusAsync(
            id,
            corpusId,
            corpusConfig.SourceFiles is null
                ? null
                : await MapAsync(getDataFileHandler, corpusConfig.SourceFiles, cancellationToken),
            corpusConfig.TargetFiles is null
                ? null
                : await MapAsync(getDataFileHandler, corpusConfig.TargetFiles, cancellationToken),
            cancellationToken
        );
        return Ok(Map(id, corpus));
    }

    /// <summary>
    /// Get all corpora for a translation engine (obsolete - use parallel corpora instead)
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpora</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine</response>
    /// <response code="404">The engine does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<TranslationCorpusDto>>> GetAllCorporaAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        return Ok(engine.Corpora.Select(c => Map(id, c)));
    }

    /// <summary>
    /// Get the configuration of a corpus for a translation engine (obsolete - use parallel corpora instead)
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus configuration</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}", Name = Endpoints.GetTranslationCorpus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationCorpusDto>> GetCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        Corpus? corpus = engine.Corpora.FirstOrDefault(f => f.Id == corpusId);
        if (corpus == null)
            return NotFound();
        return Ok(Map(id, corpus));
    }

    /// <summary>
    /// Remove a corpus from a translation engine (obsolete - use parallel corpora instead)
    /// </summary>
    /// <remarks>
    /// Removing a corpus will remove all pretranslations associated with that corpus.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="deleteFiles">If `true`, all files associated with the corpus will be deleted as well (even if they are associated with other corpora). If false, no files will be deleted.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was deleted successfully.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpDelete("{id}/corpora/{corpusId}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromQuery(Name = "delete-files")] bool? deleteFiles,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        await _engineService.DeleteCorpusAsync(id, corpusId, deleteFiles ?? false, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Get all pretranslations in a corpus or parallel corpus of a translation engine
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
    /// <param name="corpusId">The corpus id or parallel corpus id</param>
    /// <param name="textId">The text id (optional)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The pretranslations</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}/pretranslations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<PretranslationDto>>> GetAllCorpusPretranslationsAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromQuery(Name = "text-id")] string? textId,
        [OpenApiIgnore] [FromQuery(Name = "textId")] string? textIdCamelCase,
        CancellationToken cancellationToken
    )
    {
        textId ??= textIdCamelCase;
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.Corpora.Any(c => c.Id == corpusId) && !engine.ParallelCorpora.Any(c => c.Id == corpusId))
            return NotFound();
        if (engine.ModelRevision == 0)
            return Conflict();

        IEnumerable<Pretranslation> pretranslations = await _pretranslationService.GetAllAsync(
            id,
            engine.ModelRevision,
            corpusId,
            textId,
            cancellationToken
        );
        _logger.LogInformation(
            "Returning {Count} pretranslations for engine {EngineId}, corpus {CorpusId}, and query {TextId}",
            pretranslations.Count(),
            id,
            corpusId,
            textId
        );
        return Ok(pretranslations.Select(Map));
    }

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
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.Corpora.Any(c => c.Id == corpusId) && !engine.ParallelCorpora.Any(c => c.Id == corpusId))
            return NotFound();
        if (engine.ModelRevision == 0)
            return Conflict();

        IEnumerable<Pretranslation> pretranslations = await _pretranslationService.GetAllAsync(
            id,
            engine.ModelRevision,
            corpusId,
            textId,
            cancellationToken
        );
        _logger.LogInformation(
            "Returning {Count} pretranslations for engine {EngineId}, corpus {CorpusId}, and textId {TextId}",
            pretranslations.Count(),
            id,
            corpusId,
            textId
        );
        return Ok(pretranslations.Select(Map));
    }

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
    /// * `PushToEnd`: The USFM markers (or the entire embed) are preserved and placed at the end of the verse. **This is the default for paragraph markers**.
    /// * `TryToPlace`: The USFM markers (or the entire embed) are placed in approximately the right location within the verse. **This option is only available for paragraph markers. Quality of placement may differ from language to language. Only works when `template` is set to `Source`**.
    /// * `Strip`: The USFM markers (or the entire embed) are removed. **This is the default for embeds and style markers**.
    ///
    /// Quote normalization behavior is controlled by the `quote-normalization-behavior` parameter options:
    /// * `Normalized`: The quotes in the pretranslated USFM are normalized quotes (typically straight quotes: ', ") in the style of the source data. **This is the default**.
    /// * `Denormalized`: The quotes in the pretranslated USFM are denormalized into the style of the target data. Quote denormalization may not be successful in all contexts. A remark will be added to the USFM listing the chapters that were successfully denormalized.
    ///
    /// Only pretranslations for the most recent successful build of the engine are returned.
    /// The USFM parsing and marker types used are defined here: [this wiki](https://github.com/sillsdev/serval/wiki/USFM-Parsing-and-Translation).
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id or parallel corpus id</param>
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
    /// <response code="400">The corpus is not a valid Scripture corpus.</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}/pretranslations/{textId}/usfm")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetCorpusPretranslatedUsfmAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [NotNull] string textId,
        [FromQuery(Name = "text-origin")] PretranslationUsfmTextOrigin? textOrigin,
        [FromQuery] PretranslationUsfmTemplate? template,
        [FromQuery(Name = "paragraph-marker-behavior")] PretranslationUsfmMarkerBehavior? paragraphMarkerBehavior,
        [FromQuery(Name = "embed-behavior")] PretranslationUsfmMarkerBehavior? embedBehavior,
        [FromQuery(Name = "style-marker-behavior")] PretranslationUsfmMarkerBehavior? styleMarkerBehavior,
        [FromQuery(Name = "quotation-mark-behavior")] PretranslationNormalizationBehavior? quoteNormalizationBehavior,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.Corpora.Any(c => c.Id == corpusId) && !engine.ParallelCorpora.Any(c => c.Id == corpusId))
            return NotFound();
        if (engine.ModelRevision == 0)
            return Conflict();

        string usfm = await _pretranslationService.GetUsfmAsync(
            id,
            engine.ModelRevision,
            corpusId,
            textId,
            textOrigin ?? PretranslationUsfmTextOrigin.PreferExisting,
            template ?? PretranslationUsfmTemplate.Auto,
            paragraphMarkerBehavior ?? PretranslationUsfmMarkerBehavior.Preserve,
            embedBehavior ?? PretranslationUsfmMarkerBehavior.Strip,
            styleMarkerBehavior ?? PretranslationUsfmMarkerBehavior.Strip,
            quoteNormalizationBehavior ?? PretranslationNormalizationBehavior.Normalized,
            cancellationToken
        );
        if (usfm == "")
            return NoContent();
        _logger.LogInformation(
            "Returning USFM for {TextId} in engine {EngineId} for corpus {corpusId}",
            textId,
            id,
            corpusId
        );
        return Content(usfm, "text/plain");
    }

    /// <summary>
    /// Add a parallel corpus to a translation engine
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **`sourceCorpusIds`**: The source corpora associated with the parallel corpus
    /// * **`targetCorpusIds`**: The target corpora associated with the parallel corpus
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusConfig">The corpus configuration (see remarks)</param>
    /// <param name="getCorpusHandler"></param>
    /// <param name="idGenerator"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The added corpus</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/parallel-corpora")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationParallelCorpusDto>> AddParallelCorpusAsync(
        [NotNull] string id,
        [FromBody] TranslationParallelCorpusConfigDto corpusConfig,
        [FromServices] IRequestHandler<GetCorpus, GetCorpusResponse> getCorpusHandler,
        [FromServices] IIdGenerator idGenerator,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        ParallelCorpus corpus = await MapAsync(
            getCorpusHandler,
            idGenerator.GenerateId(),
            corpusConfig,
            cancellationToken
        );
        await _engineService.AddParallelCorpusAsync(id, corpus, cancellationToken);
        TranslationParallelCorpusDto dto = Map(id, corpus);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Update a parallel corpus with a new set of corpora
    /// </summary>
    /// <remarks>
    /// Will completely replace the parallel corpus' file associations. Will not affect jobs already queued or running. Will not affect existing pretranslations until new build is complete.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="corpusConfig">The corpus configuration</param>
    /// <param name="getCorpusHandler">The data file client</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was updated successfully</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPatch("{id}/parallel-corpora/{parallelCorpusId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationParallelCorpusDto>> UpdateParallelCorpusAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        [FromBody] TranslationParallelCorpusUpdateConfigDto corpusConfig,
        [FromServices] IRequestHandler<GetCorpus, GetCorpusResponse> getCorpusHandler,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        ParallelCorpus parallelCorpus = await _engineService.UpdateParallelCorpusAsync(
            id,
            parallelCorpusId,
            corpusConfig.SourceCorpusIds is null
                ? null
                : await MapAsync(getCorpusHandler, corpusConfig.SourceCorpusIds, cancellationToken),
            corpusConfig.TargetCorpusIds is null
                ? null
                : await MapAsync(getCorpusHandler, corpusConfig.TargetCorpusIds, cancellationToken),
            cancellationToken
        );
        return Ok(Map(id, parallelCorpus));
    }

    /// <summary>
    /// Get all parallel corpora for a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The parallel corpora</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine</response>
    /// <response code="404">The engine does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/parallel-corpora")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<TranslationParallelCorpusDto>>> GetAllParallelCorporaAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        return Ok(engine.ParallelCorpora.Select(c => Map(id, c)));
    }

    /// <summary>
    /// Get the configuration of a parallel corpus for a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The parallel corpus configuration</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/parallel-corpora/{parallelCorpusId}", Name = Endpoints.GetParallelTranslationCorpus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationParallelCorpusDto>> GetParallelCorpusAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        ParallelCorpus? corpus = engine.ParallelCorpora.FirstOrDefault(f => f.Id == parallelCorpusId);
        if (corpus == null)
            return NotFound();
        return Ok(Map(id, corpus));
    }

    /// <summary>
    /// Remove a parallel corpus from a translation engine
    /// </summary>
    /// <remarks>
    /// Removing a parallel corpus will remove all pretranslations associated with that corpus.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The parallel corpus was deleted successfully.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpDelete("{id}/parallel-corpora/{parallelCorpusId}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteParallelCorpusAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        await _engineService.DeleteParallelCorpusAsync(id, parallelCorpusId, cancellationToken);
        return Ok();
    }

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
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.ParallelCorpora.Any(c => c.Id == parallelCorpusId))
            return NotFound();
        if (engine.ModelRevision == 0)
            return Conflict();

        IEnumerable<Pretranslation> pretranslations = await _pretranslationService.GetAllAsync(
            id,
            engine.ModelRevision,
            parallelCorpusId,
            textId,
            cancellationToken
        );
        _logger.LogInformation(
            "Returning {Count} pretranslations for engine {EngineId}, parallel corpus {ParallelCorpusId}, and query {TextId}",
            pretranslations.Count(),
            id,
            parallelCorpusId,
            textId
        );
        return Ok(pretranslations.Select(Map));
    }

    /// <summary>
    /// Get all pretranslations for the specified text in a parallel corpus of a translation engine
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
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="textId">The text id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The pretranslations</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/parallel-corpora/{parallelCorpusId}/pretranslations/{textId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<PretranslationDto>>> GetPretranslationsByTextIdAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        [NotNull] string textId,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.ParallelCorpora.Any(c => c.Id == parallelCorpusId))
            return NotFound();
        if (engine.ModelRevision == 0)
            return Conflict();

        IEnumerable<Pretranslation> pretranslations = await _pretranslationService.GetAllAsync(
            id,
            engine.ModelRevision,
            parallelCorpusId,
            textId,
            cancellationToken
        );
        _logger.LogInformation(
            "Returning {Count} pretranslations for engine {EngineId}, parallel corpus {ParallelCorpusId}, and textId {TextId}",
            pretranslations.Count(),
            id,
            parallelCorpusId,
            textId
        );
        return Ok(pretranslations.Select(Map));
    }

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
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.ParallelCorpora.Any(c => c.Id == parallelCorpusId))
            return NotFound();
        if (engine.ModelRevision == 0)
            return Conflict();

        string usfm = await _pretranslationService.GetUsfmAsync(
            id,
            engine.ModelRevision,
            parallelCorpusId,
            textId,
            textOrigin ?? PretranslationUsfmTextOrigin.PreferExisting,
            template ?? PretranslationUsfmTemplate.Auto,
            paragraphMarkerBehavior ?? PretranslationUsfmMarkerBehavior.Preserve,
            embedBehavior ?? PretranslationUsfmMarkerBehavior.Preserve,
            styleMarkerBehavior ?? PretranslationUsfmMarkerBehavior.Strip,
            quoteNormalizationBehavior ?? PretranslationNormalizationBehavior.Normalized,
            cancellationToken
        );
        if (usfm == "")
            return NoContent();
        _logger.LogInformation(
            "Returning USFM for {TextId} in engine {EngineId} for parallel corpus {ParallelCorpusId}",
            textId,
            id,
            parallelCorpusId
        );
        return Content(usfm, "text/plain");
    }

    /// <summary>
    /// Get all build jobs for a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build jobs</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<TranslationBuildDto>>> GetAllBuildsAsync(
        [NotNull] string id,
        [FromServices] DtoMapper mapper,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        return Ok((await _buildService.GetAllAsync(Owner, id, cancellationToken)).Select(mapper.Map));
    }

    /// <summary>
    /// Get a build job
    /// </summary>
    /// <remarks>
    /// If the `minRevision` is not defined, the current build, at whatever state it is,
    /// will be immediately returned.  If `minRevision` is defined, Serval will wait for
    /// up to 40 seconds for the engine to build to the `minRevision` specified, else
    /// will timeout.
    /// A use case is to actively query the state of the current build, where the subsequent
    /// request sets the `minRevision` to the returned `revision` + 1 and timeouts are handled gracefully.
    /// This method should use request throttling.
    /// Note: Within the returned build, progress is a value between 0 and 1.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="buildId">The build job id</param>
    /// <param name="minRevision">The minimum revision</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build job</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="404">The engine or build does not exist.</response>
    /// <response code="408">The long polling request timed out. This is expected behavior if you're using long-polling with the minRevision strategy specified in the docs.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/builds/{buildId}", Name = Endpoints.GetTranslationBuild)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationBuildDto>> GetBuildAsync(
        [NotNull] string id,
        [NotNull] string buildId,
        [FromQuery(Name = "min-revision")] long? minRevision,
        [OpenApiIgnore] [FromQuery(Name = "minRevision")] long? minRevisionCamelCase,
        [FromServices] DtoMapper mapper,
        CancellationToken cancellationToken
    )
    {
        minRevision ??= minRevisionCamelCase;
        await AuthorizeAsync(id, cancellationToken);
        if (minRevision != null)
        {
            (_, EntityChange<Build> change) = await TaskEx.Timeout(
                ct => _buildService.GetNewerRevisionAsync(buildId, minRevision.Value, ct),
                _apiOptions.CurrentValue.LongPollTimeout,
                cancellationToken: cancellationToken
            );
            return change.Type switch
            {
                EntityChangeType.None => StatusCode(StatusCodes.Status408RequestTimeout),
                EntityChangeType.Delete => NotFound(),
                _ => Ok(mapper.Map(change.Entity!)),
            };
        }
        else
        {
            Build build = await _buildService.GetAsync(buildId, cancellationToken);
            return Ok(mapper.Map(build));
        }
    }

    /// <summary>
    /// Get the currently running build job for a translation engine
    /// </summary>
    /// <remarks>
    /// See documentation on endpoint /translation/engines/{id}/builds/{id} - "Get a Build Job" for details on using `minRevision`.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="minRevision">The minimum revision</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build job</response>
    /// <response code="204">There is no build currently running.</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="408">The long polling request timed out. This is expected behavior if you're using long-polling with the minRevision strategy specified in the docs.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/current-build")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationBuildDto>> GetCurrentBuildAsync(
        [NotNull] string id,
        [FromQuery(Name = "min-revision")] long? minRevision,
        [OpenApiIgnore] [FromQuery(Name = "minRevision")] long? minRevisionCamelCase,
        [FromServices] DtoMapper mapper,
        CancellationToken cancellationToken
    )
    {
        minRevision ??= minRevisionCamelCase;
        await AuthorizeAsync(id, cancellationToken);
        if (minRevision != null)
        {
            (_, EntityChange<Build> change) = await TaskEx.Timeout(
                ct => _buildService.GetActiveNewerRevisionAsync(id, minRevision.Value, ct),
                _apiOptions.CurrentValue.LongPollTimeout,
                cancellationToken: cancellationToken
            );
            return change.Type switch
            {
                EntityChangeType.None => StatusCode(StatusCodes.Status408RequestTimeout),
                EntityChangeType.Delete => NoContent(),
                _ => Ok(mapper.Map(change.Entity!)),
            };
        }
        else
        {
            Build? build = await _buildService.GetActiveAsync(id, cancellationToken);
            if (build == null)
                return NoContent();

            return Ok(mapper.Map(build));
        }
    }

    private async Task AuthorizeAsync(string id, CancellationToken cancellationToken)
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
    }

    private async Task<Corpus> MapAsync(
        IRequestHandler<GetDataFile, GetDataFileResponse> getDataFileHandler,
        string corpusId,
        TranslationCorpusConfigDto source,
        CancellationToken cancellationToken
    )
    {
        return new Corpus
        {
            Id = corpusId,
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = await MapAsync(getDataFileHandler, source.SourceFiles, cancellationToken),
            TargetFiles = await MapAsync(getDataFileHandler, source.TargetFiles, cancellationToken),
        };
    }

    private async Task<ParallelCorpus> MapAsync(
        IRequestHandler<GetCorpus, GetCorpusResponse> getCorpusHandler,
        string corpusId,
        TranslationParallelCorpusConfigDto source,
        CancellationToken cancellationToken
    )
    {
        return new ParallelCorpus
        {
            Id = corpusId,
            SourceCorpora = await MapAsync(getCorpusHandler, source.SourceCorpusIds, cancellationToken),
            TargetCorpora = await MapAsync(getCorpusHandler, source.TargetCorpusIds, cancellationToken),
        };
    }

    private async Task<List<CorpusFile>> MapAsync(
        IRequestHandler<GetDataFile, GetDataFileResponse> getDataFileHandler,
        IEnumerable<TranslationCorpusFileConfigDto> fileConfigs,
        CancellationToken cancellationToken
    )
    {
        var files = new List<CorpusFile>();
        foreach (TranslationCorpusFileConfigDto fileConfig in fileConfigs)
        {
            GetDataFileResponse response = await getDataFileHandler.HandleAsync(
                new(fileConfig.FileId, Owner),
                cancellationToken
            );
            if (response.IsFound)
            {
                files.Add(
                    new CorpusFile
                    {
                        Id = fileConfig.FileId,
                        Filename = response.File.Filename,
                        TextId = fileConfig.TextId ?? response.File.Name,
                        Format = response.File.Format,
                    }
                );
            }
            else
            {
                throw new InvalidOperationException($"The data file {fileConfig.FileId} cannot be found.");
            }
        }
        return files;
    }

    private async Task<List<MonolingualCorpus>> MapAsync(
        IRequestHandler<GetCorpus, GetCorpusResponse> getCorpusHandler,
        IEnumerable<string> corpusIds,
        CancellationToken cancellationToken
    )
    {
        var corpora = new List<MonolingualCorpus>();
        foreach (string corpusId in corpusIds)
        {
            GetCorpusResponse response = await getCorpusHandler.HandleAsync(new(corpusId, Owner), cancellationToken);
            if (response.IsFound)
            {
                if (!response.Corpus.Files.Any())
                {
                    throw new InvalidOperationException(
                        $"The corpus {corpusId} does not have any files associated with it."
                    );
                }
                corpora.Add(
                    new MonolingualCorpus
                    {
                        Id = corpusId,
                        Name = response.Corpus.Name ?? "",
                        Language = response.Corpus.Language,
                        Files =
                        [
                            .. response.Corpus.Files.Select(f => new CorpusFile
                            {
                                Id = f.File.DataFileId,
                                Filename = f.File.Filename,
                                Format = f.File.Format,
                                TextId = f.TextId ?? f.File.Name,
                            }),
                        ],
                    }
                );
            }
            else
            {
                throw new InvalidOperationException($"The corpus {corpusId} cannot be found.");
            }
        }
        return corpora;
    }

    private static PretranslationDto Map(Pretranslation source)
    {
        return new PretranslationDto
        {
            TextId = source.TextId,
            SourceRefs = source.SourceRefs ?? [],
            TargetRefs = source.TargetRefs ?? [],
            Refs = source.Refs,
            Translation = source.Translation,
            Confidence = source.Confidence ?? -1.0,
        };
    }

    private TranslationCorpusDto Map(string engineId, Corpus source)
    {
        return new TranslationCorpusDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(Endpoints.GetTranslationCorpus, new { id = engineId, corpusId = source.Id }),
            Engine = new ResourceLinkDto
            {
                Id = engineId,
                Url = _urlService.GetUrl(Endpoints.GetTranslationEngine, new { id = engineId }),
            },
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = source.SourceFiles.Select(Map).ToList(),
            TargetFiles = source.TargetFiles.Select(Map).ToList(),
        };
    }

    private TranslationParallelCorpusDto Map(string engineId, ParallelCorpus source)
    {
        return new TranslationParallelCorpusDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(Endpoints.GetCorpus, new { id = engineId, corpusId = source.Id }),
            Engine = new ResourceLinkDto
            {
                Id = engineId,
                Url = _urlService.GetUrl(Endpoints.GetTranslationEngine, new { id = engineId }),
            },
            SourceCorpora = source
                .SourceCorpora.Select(c => new ResourceLinkDto
                {
                    Id = c.Id,
                    Url = _urlService.GetUrl(Endpoints.GetCorpus, new { Id = c.Id }),
                })
                .ToList(),
            TargetCorpora = source
                .TargetCorpora.Select(c => new ResourceLinkDto
                {
                    Id = c.Id,
                    Url = _urlService.GetUrl(Endpoints.GetCorpus, new { Id = c.Id }),
                })
                .ToList(),
        };
    }

    private TranslationCorpusFileDto Map(CorpusFile source)
    {
        return new TranslationCorpusFileDto
        {
            File = new ResourceLinkDto
            {
                Id = source.Id,
                Url = _urlService.GetUrl(Endpoints.GetDataFile, new { id = source.Id }),
            },
            TextId = source.TextId,
        };
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
