namespace Serval.Translation.Controllers;

#pragma warning disable CS0612 // Type or member is obsolete

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/engines")]
[OpenApiTag("Translation Engines")]
public class TranslationEnginesController(
    IAuthorizationService authService,
    IEngineService engineService,
    IBuildService buildService,
    IPretranslationService pretranslationService,
    IOptionsMonitor<ApiOptions> apiOptions,
    IConfiguration configuration,
    IUrlService urlService,
    ILogger<TranslationEnginesController> logger
) : ServalControllerBase(authService)
{
    private static readonly JsonSerializerOptions ObjectJsonSerializerOptions =
        new() { Converters = { new ObjectToInferredTypesConverter() } };
    private readonly IEngineService _engineService = engineService;
    private readonly IBuildService _buildService = buildService;
    private readonly IPretranslationService _pretranslationService = pretranslationService;
    private readonly IOptionsMonitor<ApiOptions> _apiOptions = apiOptions;
    private readonly IUrlService _urlService = urlService;
    private readonly ILogger<TranslationEnginesController> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    /// <summary>
    /// Get all translation engines
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engines</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<TranslationEngineDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _engineService.GetAllAsync(Owner, cancellationToken)).Select(Map);
    }

    /// <summary>
    /// Get a translation engine by unique id
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The translation engine</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>

    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}", Name = Endpoints.GetTranslationEngine)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationEngineDto>> GetAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        return Ok(Map(engine));
    }

    /// <summary>
    /// Create a new translation engine
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **`name`**: (optional) A name to help identify and distinguish the translation engine.
    ///   * Recommendation: Create a multi-part name to distinguish between projects, uses, etc.
    ///   * The name does not have to be unique, as the engine is uniquely identified by the auto-generated id
    /// * **`sourceLanguage`**: The source language code (a valid [IETF language tag](https://en.wikipedia.org/wiki/IETF_language_tag) is recommended)
    /// * **`targetLanguage`**: The target language code (a valid IETF language tag is recommended)
    /// * **`type`**: **`smt-transfer`** or **`nmt`** or **`echo`**
    /// * **`isModelPersisted`**: (optional) - see below
    /// ### smt-transfer
    /// The Statistical Machine Translation Transfer Learning engine is primarily used for translation suggestions. Typical endpoints: translate, get-word-graph, train-segment
    /// * **`isModelPersisted`**: (default to `true`) All models are persistent and can be updated with train-segment.  False is not supported.
    /// ### nmt
    /// The Neural Machine Translation engine is primarily used for pretranslations.  It is fine-tuned from Meta's NLLB-200. Valid IETF language tags provided to Serval will be converted to [NLLB-200 codes](https://github.com/facebookresearch/flores/tree/main/flores200#languages-in-flores-200).  See more about language tag resolution [here](https://github.com/sillsdev/serval/wiki/FLORES%E2%80%90200-Language-Code-Resolution-for-NMT-Engine).
    /// * **`isModelPersisted`**: (default to `false`) Whether the model can be downloaded by the client after it has been successfully built.
    ///
    /// If you use a language among NLLB's supported languages, Serval will utilize everything the NLLB-200 model already knows about that language when translating. If the language you are working with is not among NLLB's supported languages, the language code will have no effect.
    ///
    /// Typical endpoints: pretranslate
    /// ### echo
    /// The echo engine has full coverage of all nmt and smt-transfer endpoints. Endpoints like create and build return empty responses. Endpoints like translate and get-word-graph echo the sent content back to the user in a format that mocks nmt or smt-transfer. For example, translating a segment "test" with the echo engine would yield a translation response with translation "test". This engine is useful for debugging and testing purposes.
    /// ## Sample request:
    ///
    ///     {
    ///       "name": "myTeam:myProject:myEngine",
    ///       "sourceLanguage": "el",
    ///       "targetLanguage": "en",
    ///       "type": "nmt"
    ///       "isModelPersisted": true
    ///     }
    ///
    /// </remarks>
    /// <param name="engineConfig">The translation engine configuration (see above)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new translation engine</response>
    /// <response code="400">Bad request. Is the engine type correct?</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.CreateTranslationEngines)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationEngineDto>> CreateAsync(
        [FromBody] TranslationEngineConfigDto engineConfig,
        CancellationToken cancellationToken
    )
    {
        Engine engine = Map(engineConfig);
        Engine updatedEngine = await _engineService.CreateAsync(engine, cancellationToken);
        TranslationEngineDto dto = Map(updatedEngine);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Delete a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engine was successfully deleted.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist and therefore cannot be deleted.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.DeleteTranslationEngines)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(id, cancellationToken);
        await _engineService.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Update the source and/or target languages of a translation engine
    /// </summary>
    /// <remarks>
    /// ## Sample request:
    ///
    ///     {
    ///       "sourceLanguage": "en",
    ///       "targetLanguage": "en"
    ///     }
    ///
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engine language was successfully updated.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist and therefore cannot be updated.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] string id,
        [FromBody] TranslationEngineUpdateConfigDto request,
        CancellationToken cancellationToken = default
    )
    {
        await AuthorizeAsync(id, cancellationToken);

        if (
            request is null
            || (string.IsNullOrWhiteSpace(request.SourceLanguage) && string.IsNullOrWhiteSpace(request.TargetLanguage))
        )
        {
            return BadRequest("sourceLanguage or targetLanguage is required.");
        }

        await _engineService.UpdateAsync(
            id,
            string.IsNullOrWhiteSpace(request.SourceLanguage) ? null : request.SourceLanguage,
            string.IsNullOrWhiteSpace(request.TargetLanguage) ? null : request.TargetLanguage,
            cancellationToken
        );

        return Ok();
    }

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
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        TranslationResult? result = await _engineService.TranslateAsync(id, segment, cancellationToken);
        if (result is null)
            return Conflict();
        _logger.LogInformation("Translated segment for engine {EngineId}", id);
        return Ok(Map(result));
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
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        IEnumerable<TranslationResult>? results = await _engineService.TranslateAsync(
            id,
            n,
            segment,
            cancellationToken
        );
        if (results is null)
            return Conflict();
        _logger.LogInformation("Translated {n} segments for engine {EngineId}", n, id);
        return Ok(results.Select(Map));
    }

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
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        WordGraph? wordGraph = await _engineService.GetWordGraphAsync(id, segment, cancellationToken);
        if (wordGraph is null)
            return Conflict();
        _logger.LogInformation("Got word graph for engine {EngineId}", id);
        return Ok(Map(wordGraph));
    }

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
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        if (
            !await _engineService.TrainSegmentPairAsync(
                id,
                segmentPair.SourceSegment,
                segmentPair.TargetSegment,
                segmentPair.SentenceStart,
                cancellationToken
            )
        )
        {
            return Conflict();
        }
        _logger.LogInformation("Trained segment pair for engine {EngineId}", id);
        return Ok();
    }

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
    /// <param name="getDataFileClient"></param>
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
        [FromServices] IRequestClient<GetDataFile> getDataFileClient,
        [FromServices] IIdGenerator idGenerator,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        Corpus corpus = await MapAsync(getDataFileClient, idGenerator.GenerateId(), corpusConfig, cancellationToken);
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
    /// <param name="getDataFileClient">The data file client</param>
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
        [FromServices] IRequestClient<GetDataFile> getDataFileClient,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        Corpus corpus = await _engineService.UpdateCorpusAsync(
            id,
            corpusId,
            corpusConfig.SourceFiles is null
                ? null
                : await MapAsync(getDataFileClient, corpusConfig.SourceFiles, cancellationToken),
            corpusConfig.TargetFiles is null
                ? null
                : await MapAsync(getDataFileClient, corpusConfig.TargetFiles, cancellationToken),
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
    /// * `PushToEnd`: The USFM markers (or the entire embed) are preserved and placed at the end of the verse. **This is the default for paragraph markers and embeds**.
    /// * `TryToPlace`: The USFM markers (or the entire embed) are placed in approximately the right location within the verse. **This option is only available for paragraph markers. Quality of placement may differ from language to language. Only works when `template` is set to `Source`**.
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
            embedBehavior ?? PretranslationUsfmMarkerBehavior.Preserve,
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
    /// <param name="getCorpusClient"></param>
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
        [FromServices] IRequestClient<GetCorpus> getCorpusClient,
        [FromServices] IIdGenerator idGenerator,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        ParallelCorpus corpus = await MapAsync(
            getCorpusClient,
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
    /// <param name="getCorpusClient">The data file client</param>
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
        [FromServices] IRequestClient<GetCorpus> getCorpusClient,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        ParallelCorpus parallelCorpus = await _engineService.UpdateParallelCorpusAsync(
            id,
            parallelCorpusId,
            corpusConfig.SourceCorpusIds is null
                ? null
                : await MapAsync(getCorpusClient, corpusConfig.SourceCorpusIds, cancellationToken),
            corpusConfig.TargetCorpusIds is null
                ? null
                : await MapAsync(getCorpusClient, corpusConfig.TargetCorpusIds, cancellationToken),
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
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        return Ok((await _buildService.GetAllAsync(id, cancellationToken)).Select(Map));
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
                _ => Ok(Map(change.Entity!)),
            };
        }
        else
        {
            Build build = await _buildService.GetAsync(buildId, cancellationToken);
            return Ok(Map(build));
        }
    }

    /// <summary>
    /// Starts a build job for a translation engine.
    /// </summary>
    /// <remarks>
    /// Specify the corpora and text ids/scripture ranges within those corpora to train on. Only one type of corpus may be used: either (legacy) corpora (see /translation/engines/{id}/corpora) or parallel corpora (see /translation/engines/{id}/parallel-corpora).
    /// Specifying a corpus:
    /// * A (legacy) corpus is selected by specifying `corpusId` and a parallel corpus is selected by specifying `parallelCorpusId`.
    /// * A parallel corpus can be further filtered by specifying particular corpusIds in `sourceFilters` or `targetFilters`.
    ///
    /// Filtering by text id or chapter:
    /// * Paratext projects can be filtered by [book using the `textIds`](https://github.com/sillsdev/libpalaso/blob/master/SIL.Scripture/Canon.cs).
    /// * Filters can also be supplied via the `scriptureRange` parameter as ranges of biblical text. See [here](https://github.com/sillsdev/serval/wiki/Filtering-Paratext-Project-Data-with-a-Scripture-Range).
    /// * All Paratext project filtering follows original versification. See [here](https://github.com/sillsdev/serval/wiki/Versification-in-Serval) for more information.
    ///
    /// Filter - train on all or none
    /// * If `trainOn` or `pretranslate` is not provided, all corpora will be used for training or pretranslation respectively
    /// * If a corpus is selected for training or pretranslation and neither `scriptureRange` nor `textIds` is defined, all of the selected corpus will be used.
    /// * If a corpus is selected for training or pretranslation and an empty `scriptureRange` or `textIds` is defined, none of the selected corpus will be used.
    /// * If a corpus is selected for training or pretranslation but no further filters are provided, all selected corpora will be used for training or pretranslation respectively.
    ///
    /// Specify the corpora and text ids/scripture ranges within those corpora to pretranslate. When a corpus is selected for pretranslation,
    /// the following text will be pretranslated:
    /// * Text segments that are in the source but do not exist in the target.
    /// * Text segments that are in the source and the target, but because of `trainOn` filtering, have not been trained on.
    /// If the engine does not support pretranslation, these fields have no effect.
    /// Pretranslating uses the same filtering as training.
    ///
    /// The `options` parameter of the build config provides the ability to pass build configuration parameters as a JSON object.
    /// See [nmt job settings documentation](https://github.com/sillsdev/serval/wiki/NMT-Build-Options) about configuring job parameters.
    /// See [smt-transfer job settings documentation](https://github.com/sillsdev/serval/wiki/SMT-Transfer-Build-Options) about configuring job parameters.
    /// See [keyterms parsing documentation](https://github.com/sillsdev/serval/wiki/Paratext-Key-Terms-Parsing) on how to use keyterms for training.
    ///
    /// Note that when using a parallel corpus:
    /// * If, within a single parallel corpus, multiple source corpora have data for the same text ids (for text files or Paratext Projects) or books (for Paratext Projects only using the scripture range), those sources will be mixed where they overlap by randomly choosing from each source per line/verse.
    /// * If, within a single parallel corpus, multiple target corpora have data for the same text ids (for text files or Paratext Projects) or books (for Paratext Projects only using the scripture range), only the first of the targets that includes that text id/book will be used for that text id/book.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="buildConfig">The build config (see remarks)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new build job</response>
    /// <response code="400">The build configuration was invalid.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="409">There is already an active/pending build or a build in the process of being canceled.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationBuildDto>> StartBuildAsync(
        [NotNull] string id,
        [FromBody] TranslationBuildConfigDto buildConfig,
        CancellationToken cancellationToken
    )
    {
        string deploymentVersion = _configuration.GetValue<string>("deploymentVersion") ?? "Unknown";

        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        Build build = Map(engine, buildConfig, deploymentVersion);

        if (!await _engineService.StartBuildAsync(build, cancellationToken))
            return Conflict();

        TranslationBuildDto dto = Map(build);
        return Created(dto.Url, dto);
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
                _ => Ok(Map(change.Entity!)),
            };
        }
        else
        {
            Build? build = await _buildService.GetActiveAsync(id, cancellationToken);
            if (build == null)
                return NoContent();

            return Ok(Map(build));
        }
    }

    /// <summary>
    /// Cancel the current build job (whether pending or active) for a translation engine
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build job was cancelled successfully.</response>
    /// <response code="204">There is no active build job.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The translation engine does not support cancelling builds.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/current-build/cancel")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationBuildDto>> CancelBuildAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        Build? build = await _engineService.CancelBuildAsync(id, cancellationToken);
        if (build is null)
            return NoContent();
        return Ok(Map(build));
    }

    /// <summary>
    /// Get a link to download the NMT translation model of the last build that was successfully saved.
    /// </summary>
    /// <remarks>
    /// If an nmt build was successful and `isModelPersisted` is `true` for the engine,
    /// then the model from the most recent successful build can be downloaded.
    ///
    /// The endpoint will return a URL that can be used to download the model for up to 1 hour
    /// after the request is made.  If the URL is not used within that time, a new request will need to be made.
    ///
    /// The download itself is created by g-zipping together the folder containing the fine tuned model
    /// with all necessary supporting files.  This zipped folder is then named by the pattern:
    ///  * &lt;engine_id&gt;_&lt;model_revision&gt;.tar.gz
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The url to download the model.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="404">The engine does not exist or there is no saved model.</response>
    /// <response code="405">The translation engine does not support downloading builds.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/model-download-url")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ModelDownloadUrlDto>> GetModelDownloadUrlAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        ModelDownloadUrl? modelInfo = await _engineService.GetModelDownloadUrlAsync(id, cancellationToken);
        if (modelInfo is null)
            return NotFound();
        return Ok(Map(modelInfo));
    }

    private async Task AuthorizeAsync(string id, CancellationToken cancellationToken)
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
    }

    private async Task<Corpus> MapAsync(
        IRequestClient<GetDataFile> getDataFileClient,
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
            SourceFiles = await MapAsync(getDataFileClient, source.SourceFiles, cancellationToken),
            TargetFiles = await MapAsync(getDataFileClient, source.TargetFiles, cancellationToken)
        };
    }

    private async Task<ParallelCorpus> MapAsync(
        IRequestClient<GetCorpus> getDataFileClient,
        string corpusId,
        TranslationParallelCorpusConfigDto source,
        CancellationToken cancellationToken
    )
    {
        return new ParallelCorpus
        {
            Id = corpusId,
            SourceCorpora = await MapAsync(getDataFileClient, source.SourceCorpusIds, cancellationToken),
            TargetCorpora = await MapAsync(getDataFileClient, source.TargetCorpusIds, cancellationToken)
        };
    }

    private async Task<List<CorpusFile>> MapAsync(
        IRequestClient<GetDataFile> getDataFileClient,
        IEnumerable<TranslationCorpusFileConfigDto> fileConfigs,
        CancellationToken cancellationToken
    )
    {
        var files = new List<CorpusFile>();
        foreach (TranslationCorpusFileConfigDto fileConfig in fileConfigs)
        {
            Response<DataFileResult, DataFileNotFound> response = await getDataFileClient.GetResponse<
                DataFileResult,
                DataFileNotFound
            >(new GetDataFile { DataFileId = fileConfig.FileId, Owner = Owner }, cancellationToken);
            if (response.Is(out Response<DataFileResult>? result))
            {
                files.Add(
                    new CorpusFile
                    {
                        Id = fileConfig.FileId,
                        Filename = result.Message.Filename,
                        TextId = fileConfig.TextId ?? result.Message.Name,
                        Format = result.Message.Format
                    }
                );
            }
            else if (response.Is(out Response<DataFileNotFound>? _))
            {
                throw new InvalidOperationException($"The data file {fileConfig.FileId} cannot be found.");
            }
        }
        return files;
    }

    private async Task<List<MonolingualCorpus>> MapAsync(
        IRequestClient<GetCorpus> getCorpusClient,
        IEnumerable<string> corpusIds,
        CancellationToken cancellationToken
    )
    {
        var corpora = new List<MonolingualCorpus>();
        foreach (string corpusId in corpusIds)
        {
            Response<CorpusResult, CorpusNotFound> response = await getCorpusClient.GetResponse<
                CorpusResult,
                CorpusNotFound
            >(new GetCorpus { CorpusId = corpusId, Owner = Owner }, cancellationToken);
            if (response.Is(out Response<CorpusResult>? result))
            {
                if (!result.Message.Files.Any())
                {
                    throw new InvalidOperationException(
                        $"The corpus {corpusId} does not have any files associated with it."
                    );
                }
                corpora.Add(
                    new MonolingualCorpus
                    {
                        Id = corpusId,
                        Name = result.Message.Name ?? "",
                        Language = result.Message.Language,
                        Files = result
                            .Message.Files.Select(f => new CorpusFile
                            {
                                Id = f.File.DataFileId,
                                Filename = f.File.Filename,
                                Format = f.File.Format,
                                TextId = f.TextId ?? f.File.Name
                            })
                            .ToList(),
                    }
                );
            }
            else if (response.Is(out Response<CorpusNotFound>? _))
            {
                throw new InvalidOperationException($"The corpus {corpusId} cannot be found.");
            }
        }
        return corpora;
    }

    private Engine Map(TranslationEngineConfigDto source)
    {
        return new Engine
        {
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            Type = source.Type.ToPascalCase(),
            Owner = Owner,
            Corpora = [],
            IsModelPersisted = source.IsModelPersisted
        };
    }

    private static Build Map(Engine engine, TranslationBuildConfigDto source, string deploymentVersion)
    {
        return new Build
        {
            EngineRef = engine.Id,
            Name = source.Name,
            Pretranslate = Map(engine, source.Pretranslate),
            TrainOn = Map(engine, source.TrainOn),
            Options = Map(source.Options),
            DeploymentVersion = deploymentVersion
        };
    }

    private static List<PretranslateCorpus>? Map(Engine engine, IReadOnlyList<PretranslateCorpusConfigDto>? source)
    {
        if (source is null)
            return null;

        var corpusIds = new HashSet<string>(engine.Corpora.Select(c => c.Id));
        var parallelCorpusIds = new HashSet<string>(engine.ParallelCorpora.Select(c => c.Id));
        var pretranslateCorpora = new List<PretranslateCorpus>();
        foreach (PretranslateCorpusConfigDto pcc in source)
        {
            if (pcc.CorpusId != null)
            {
                if (pcc.ParallelCorpusId != null)
                {
                    throw new InvalidOperationException($"Only one of ParallelCorpusId and CorpusId can be set.");
                }
                if (!corpusIds.Contains(pcc.CorpusId))
                {
                    throw new InvalidOperationException(
                        $"The corpus {pcc.CorpusId} is not valid: This corpus does not exist for engine {engine.Id}."
                    );
                }
                Corpus corpus = engine.Corpora.Single(c => c.Id == pcc.CorpusId);
                if (corpus.SourceFiles.Count == 0 && corpus.TargetFiles.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The corpus {pcc.CorpusId} is not valid: This corpus does not have any source or target files."
                    );
                }
                if (pcc.TextIds != null && pcc.ScriptureRange != null)
                {
                    throw new InvalidOperationException(
                        $"The corpus {pcc.CorpusId} is not valid: Set at most one of TextIds and ScriptureRange."
                    );
                }
                pretranslateCorpora.Add(
                    new PretranslateCorpus
                    {
                        CorpusRef = pcc.CorpusId,
                        TextIds = pcc.TextIds?.ToList(),
                        ScriptureRange = pcc.ScriptureRange
                    }
                );
            }
            else
            {
                if (pcc.ParallelCorpusId == null)
                {
                    throw new InvalidOperationException($"One of ParallelCorpusId and CorpusId must be set.");
                }
                if (!parallelCorpusIds.Contains(pcc.ParallelCorpusId))
                {
                    throw new InvalidOperationException(
                        $"The parallel corpus {pcc.ParallelCorpusId} is not valid: This parallel corpus does not exist for engine {engine.Id}."
                    );
                }
                ParallelCorpus corpus = engine.ParallelCorpora.Single(pc => pc.Id == pcc.ParallelCorpusId);
                if (corpus.SourceCorpora.Count == 0 && corpus.TargetCorpora.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The corpus {pcc.ParallelCorpusId} does not have source or target corpora associated with it."
                    );
                }
                if (
                    pcc.SourceFilters != null
                    && pcc.SourceFilters.Count > 0
                    && (
                        pcc.SourceFilters.Select(sf => sf.CorpusId).Distinct().Count() > 1
                        || pcc.SourceFilters[0].CorpusId
                            != engine.ParallelCorpora.Single(pc => pc.Id == pcc.ParallelCorpusId).SourceCorpora[0].Id
                    )
                )
                {
                    throw new InvalidOperationException(
                        $"Only the first source corpus in a parallel corpus may be filtered for pretranslation."
                    );
                }
                pretranslateCorpora.Add(
                    new PretranslateCorpus
                    {
                        ParallelCorpusRef = pcc.ParallelCorpusId,
                        SourceFilters = pcc.SourceFilters?.Select(Map).ToList()
                    }
                );
            }
        }
        return pretranslateCorpora;
    }

    private static List<TrainingCorpus>? Map(Engine engine, IReadOnlyList<TrainingCorpusConfigDto>? source)
    {
        if (source is null)
            return null;

        var corpusIds = new HashSet<string>(engine.Corpora.Select(c => c.Id));
        var parallelCorpusIds = new HashSet<string>(engine.ParallelCorpora.Select(c => c.Id));
        var trainOnCorpora = new List<TrainingCorpus>();
        foreach (TrainingCorpusConfigDto tcc in source)
        {
            if (tcc.CorpusId != null)
            {
                if (tcc.ParallelCorpusId != null)
                {
                    throw new InvalidOperationException($"Only one of ParallelCorpusId and CorpusId can be set.");
                }
                if (!corpusIds.Contains(tcc.CorpusId))
                {
                    throw new InvalidOperationException(
                        $"The corpus {tcc.CorpusId} is not valid: This corpus does not exist for engine {engine.Id}."
                    );
                }
                Corpus corpus = engine.Corpora.Single(c => c.Id == tcc.CorpusId);
                if (corpus.SourceFiles.Count == 0 && corpus.TargetFiles.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The corpus {tcc.CorpusId} is not valid: This corpus does not have any source or target files."
                    );
                }
                if (tcc.TextIds != null && tcc.ScriptureRange != null)
                {
                    throw new InvalidOperationException(
                        $"The corpus {tcc.CorpusId} is not valid: Set at most one of TextIds and ScriptureRange."
                    );
                }
                trainOnCorpora.Add(
                    new TrainingCorpus
                    {
                        CorpusRef = tcc.CorpusId,
                        TextIds = tcc.TextIds?.ToList(),
                        ScriptureRange = tcc.ScriptureRange
                    }
                );
            }
            else
            {
                if (tcc.ParallelCorpusId == null)
                {
                    throw new InvalidOperationException($"One of ParallelCorpusId and CorpusId must be set.");
                }
                if (!parallelCorpusIds.Contains(tcc.ParallelCorpusId))
                {
                    throw new InvalidOperationException(
                        $"The parallel corpus {tcc.ParallelCorpusId} is not valid: This parallel corpus does not exist for engine {engine.Id}."
                    );
                }
                ParallelCorpus corpus = engine.ParallelCorpora.Single(pc => pc.Id == tcc.ParallelCorpusId);
                if (corpus.SourceCorpora.Count == 0 && corpus.TargetCorpora.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The corpus {tcc.ParallelCorpusId} does not have source or target corpora associated with it."
                    );
                }
                foreach (MonolingualCorpus monolingualCorpus in corpus.SourceCorpora.Concat(corpus.TargetCorpora))
                {
                    if (monolingualCorpus.Files.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"The corpus {monolingualCorpus.Id} referenced in parallel corpus {corpus.Id} does not have any files associated with it."
                        );
                    }
                }
                trainOnCorpora.Add(
                    new TrainingCorpus
                    {
                        ParallelCorpusRef = tcc.ParallelCorpusId,
                        SourceFilters = tcc.SourceFilters?.Select(Map).ToList(),
                        TargetFilters = tcc.TargetFilters?.Select(Map).ToList()
                    }
                );
            }
        }
        return trainOnCorpora;
    }

    private static ParallelCorpusFilter Map(ParallelCorpusFilterConfigDto source)
    {
        if (source.TextIds != null && source.ScriptureRange != null)
        {
            throw new InvalidOperationException(
                $"The parallel corpus filter for corpus {source.CorpusId} is not valid: At most, one of TextIds and ScriptureRange can be set."
            );
        }
        return new ParallelCorpusFilter
        {
            CorpusRef = source.CorpusId,
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange
        };
    }

    private static Dictionary<string, object>? Map(object? source)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(
                source?.ToString() ?? "{}",
                ObjectJsonSerializerOptions
            );
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Unable to parse field 'options' : {e.Message}", e);
        }
    }

    private TranslationEngineDto Map(Engine source)
    {
        return new TranslationEngineDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(Endpoints.GetTranslationEngine, new { id = source.Id }),
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            Type = source.Type.ToKebabCase(),
            IsModelPersisted = source.IsModelPersisted,
            IsBuilding = source.IsBuilding,
            ModelRevision = source.ModelRevision,
            Confidence = Math.Round(source.Confidence, 8),
            CorpusSize = source.CorpusSize
        };
    }

    private TranslationBuildDto Map(Build source)
    {
        return new TranslationBuildDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(Endpoints.GetTranslationBuild, new { id = source.EngineRef, buildId = source.Id }),
            Revision = source.Revision,
            Name = source.Name,
            Engine = new ResourceLinkDto
            {
                Id = source.EngineRef,
                Url = _urlService.GetUrl(Endpoints.GetTranslationEngine, new { id = source.EngineRef })
            },
            TrainOn = source.TrainOn?.Select(s => Map(source.EngineRef, s)).ToList(),
            Pretranslate = source.Pretranslate?.Select(s => Map(source.EngineRef, s)).ToList(),
            Step = source.Step,
            PercentCompleted = source.Progress,
            Progress = source.Progress,
            Message = source.Message,
            QueueDepth = source.QueueDepth,
            State = source.State,
            DateFinished = source.DateFinished,
            Options = source.Options,
            DeploymentVersion = source.DeploymentVersion,
            ExecutionData = source.ExecutionData,
            Phases = source.Phases?.Select(Map).ToList(),
            Analysis = source.Analysis?.Select(Map).ToList(),
        };
    }

    private PretranslateCorpusDto Map(string engineId, PretranslateCorpus source)
    {
        return new PretranslateCorpusDto
        {
            Corpus =
                source.CorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.CorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetTranslationCorpus,
                            new { id = engineId, corpusId = source.CorpusRef }
                        )
                    }
                    : null,
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange,
            ParallelCorpus =
                source.ParallelCorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.ParallelCorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetParallelTranslationCorpus,
                            new { id = engineId, parallelCorpusId = source.ParallelCorpusRef }
                        )
                    }
                    : null,
            SourceFilters = source.SourceFilters?.Select(Map).ToList()
        };
    }

    private TrainingCorpusDto Map(string engineId, TrainingCorpus source)
    {
        return new TrainingCorpusDto
        {
            Corpus =
                source.CorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.CorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetTranslationCorpus,
                            new { id = engineId, corpusId = source.CorpusRef }
                        )
                    }
                    : null,
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange,
            ParallelCorpus =
                source.ParallelCorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.ParallelCorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetParallelTranslationCorpus,
                            new { id = engineId, parallelCorpusId = source.ParallelCorpusRef }
                        )
                    }
                    : null,
            SourceFilters = source.SourceFilters?.Select(Map).ToList(),
            TargetFilters = source.TargetFilters?.Select(Map).ToList()
        };
    }

    private ParallelCorpusFilterDto Map(ParallelCorpusFilter source)
    {
        return new ParallelCorpusFilterDto
        {
            Corpus = new ResourceLinkDto
            {
                Id = source.CorpusRef,
                Url = _urlService.GetUrl(Endpoints.GetCorpus, new { id = source.CorpusRef })
            },
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange
        };
    }

    private TranslationResultDto Map(TranslationResult source)
    {
        return new TranslationResultDto
        {
            Translation = source.Translation,
            SourceTokens = source.SourceTokens.ToList(),
            TargetTokens = source.TargetTokens.ToList(),
            Confidences = source.Confidences.Select(c => Math.Round(c, 8)).ToList(),
            Sources = source.Sources.ToList(),
            Alignment = source.Alignment.Select(Map).ToList(),
            Phrases = source.Phrases.Select(Map).ToList()
        };
    }

    private AlignedWordPairDto Map(AlignedWordPair source)
    {
        return new AlignedWordPairDto { SourceIndex = source.SourceIndex, TargetIndex = source.TargetIndex };
    }

    private static PhraseDto Map(Phrase source)
    {
        return new PhraseDto
        {
            SourceSegmentStart = source.SourceSegmentStart,
            SourceSegmentEnd = source.SourceSegmentEnd,
            TargetSegmentCut = source.TargetSegmentCut
        };
    }

    private WordGraphDto Map(WordGraph source)
    {
        return new WordGraphDto
        {
            SourceTokens = source.SourceTokens.ToList(),
            InitialStateScore = (float)source.InitialStateScore,
            FinalStates = source.FinalStates.ToHashSet(),
            Arcs = source.Arcs.Select(Map).ToList()
        };
    }

    private WordGraphArcDto Map(WordGraphArc source)
    {
        return new WordGraphArcDto
        {
            PrevState = source.PrevState,
            NextState = source.NextState,
            Score = Math.Round(source.Score, 8),
            TargetTokens = source.TargetTokens.ToList(),
            Confidences = source.Confidences.Select(c => Math.Round(c, 8)).ToList(),
            SourceSegmentStart = source.SourceSegmentStart,
            SourceSegmentEnd = source.SourceSegmentEnd,
            Alignment = source.Alignment.Select(Map).ToList(),
            Sources = source.Sources.ToList()
        };
    }

    private static PretranslationDto Map(Pretranslation source)
    {
        return new PretranslationDto
        {
            TextId = source.TextId,
            Refs = source.Refs,
            Translation = source.Translation
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
                Url = _urlService.GetUrl(Endpoints.GetTranslationEngine, new { id = engineId })
            },
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = source.SourceFiles.Select(Map).ToList(),
            TargetFiles = source.TargetFiles.Select(Map).ToList()
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
                Url = _urlService.GetUrl(Endpoints.GetTranslationEngine, new { id = engineId })
            },
            SourceCorpora = source
                .SourceCorpora.Select(c => new ResourceLinkDto
                {
                    Id = c.Id,
                    Url = _urlService.GetUrl(Endpoints.GetCorpus, new { Id = c.Id })
                })
                .ToList(),
            TargetCorpora = source
                .TargetCorpora.Select(c => new ResourceLinkDto
                {
                    Id = c.Id,
                    Url = _urlService.GetUrl(Endpoints.GetCorpus, new { Id = c.Id })
                })
                .ToList()
        };
    }

    private TranslationCorpusFileDto Map(CorpusFile source)
    {
        return new TranslationCorpusFileDto
        {
            File = new ResourceLinkDto
            {
                Id = source.Id,
                Url = _urlService.GetUrl(Endpoints.GetDataFile, new { id = source.Id })
            },
            TextId = source.TextId
        };
    }

    private static ModelDownloadUrlDto Map(ModelDownloadUrl source)
    {
        return new ModelDownloadUrlDto
        {
            Url = source.Url,
            ModelRevision = source.ModelRevision,
            ExpiresAt = source.ExpiresAt
        };
    }

    private static PhaseDto Map(BuildPhase source)
    {
        return new PhaseDto
        {
            Stage = (PhaseStage)source.Stage,
            Step = source.Step,
            StepCount = source.StepCount
        };
    }

    private static ParallelCorpusAnalysisDto Map(ParallelCorpusAnalysis source)
    {
        return new ParallelCorpusAnalysisDto
        {
            ParallelCorpusRef = source.ParallelCorpusRef,
            SourceQuoteConvention = source.SourceQuoteConvention,
            TargetQuoteConvention = source.TargetQuoteConvention,
        };
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
