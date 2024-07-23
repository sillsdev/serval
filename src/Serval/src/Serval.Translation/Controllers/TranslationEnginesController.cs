namespace Serval.Translation.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/engines")]
[OpenApiTag("Translation Engines")]
public class TranslationEnginesController(
    IAuthorizationService authService,
    IEngineService engineService,
    IBuildService buildService,
    IPretranslationService pretranslationService,
    IOptionsMonitor<ApiOptions> apiOptions,
    IUrlService urlService
) : ServalControllerBase(authService)
{
    private static readonly JsonSerializerOptions ObjectJsonSerializerOptions =
        new() { Converters = { new ObjectToInferredTypesConverter() } };

    private readonly IEngineService _engineService = engineService;
    private readonly IBuildService _buildService = buildService;
    private readonly IPretranslationService _pretranslationService = pretranslationService;
    private readonly IOptionsMonitor<ApiOptions> _apiOptions = apiOptions;
    private readonly IUrlService _urlService = urlService;

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
    [HttpGet("{id}", Name = "GetTranslationEngine")]
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
    /// * **name**: (optional) A name to help identify and distinguish the file.
    ///   * Recommendation: Create a multi-part name to distinguish between projects, uses, etc.
    ///   * The name does not have to be unique, as the engine is uniquely identified by the auto-generated id
    /// * **sourceLanguage**: The source language code (a valid [IETF language tag](https://en.wikipedia.org/wiki/IETF_language_tag) is recommended)
    /// * **targetLanguage**: The target language code (a valid IETF language tag is recommended)
    /// * **type**: **smt-transfer** or **nmt** or **echo**
    /// * **isModelPersisted**: (optional) - see below
    /// ### smt-transfer
    /// The Statistical Machine Translation Transfer Learning engine is primarily used for translation suggestions. Typical endpoints: translate, get-word-graph, train-segment
    /// * **IsModelPersisted**: (default to true) All models are persistent and can be updated with train-segment.  False is not supported.
    /// ### nmt
    /// The Neural Machine Translation engine is primarily used for pretranslations.  It is fine-tuned from Meta's NLLB-200. Valid IETF language tags provided to Serval will be converted to [NLLB-200 codes](https://github.com/facebookresearch/flores/tree/main/flores200#languages-in-flores-200).  See more about language tag resolution [here](https://github.com/sillsdev/serval/wiki/FLORES%E2%80%90200-Language-Code-Resolution-for-NMT-Engine).
    /// * **IsModelPersisted**: (default to false) Whether the model can be downloaded by the client after it has been successfully built.
    ///
    /// If you use a language among NLLB's supported languages, Serval will utilize everything the NLLB-200 model already knows about that language when translating. If the language you are working with is not among NLLB's supported languages, the language code will have no effect.
    ///
    /// Typical endpoints: pretranslate
    /// ### echo
    /// The echo engine has full coverage of all nmt and smt-transfer endpoints. Endpoints like create and build return empty responses. Endpoints like translate and get-word-graph echo the sent content back to the user in a format that mocks nmt or Smt. For example, translating a segment "test" with the echo engine would yield a translation response with translation "test". This engine is useful for debugging and testing purposes.
    /// ## Sample request:
    ///
    ///     {
    ///       "name": "myTeam:myProject:myEngine",
    ///       "sourceLanguage": "el",
    ///       "targetLanguage": "en",
    ///       "type": "nmt"
    ///       "IsModelPersisted": true
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
        TranslationResult result = await _engineService.TranslateAsync(id, segment, cancellationToken);
        return Ok(Map(result));
    }

    /// <summary>
    /// Translates a segment of text into the top N results
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
        IEnumerable<TranslationResult> results = await _engineService.TranslateAsync(id, n, segment, cancellationToken);
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
        WordGraph wordGraph = await _engineService.GetWordGraphAsync(id, segment, cancellationToken);
        return Ok(Map(wordGraph));
    }

    /// <summary>
    /// Incrementally train a translation engine with a segment pair
    /// </summary>
    /// <remarks>
    /// A segment pair consists of a source and target segment as well as a boolean flag `sentenceStart`
    /// that should be set to true if this segment pair forms the beginning of a sentence. (This information
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
        await _engineService.TrainSegmentPairAsync(
            id,
            segmentPair.SourceSegment,
            segmentPair.TargetSegment,
            segmentPair.SentenceStart,
            cancellationToken
        );
        return Ok();
    }

    /// <summary>
    /// Add a corpus to a translation engine
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **name**: A name to help identify and distinguish the corpus from other corpora
    ///   * The name does not have to be unique since the corpus is uniquely identified by an auto-generated id
    /// * **sourceLanguage**: The source language code (See documentation on endpoint /translation/engines/ - "Create a new translation engine" for details on language codes).
    ///   * Normally, this is the same as the engine sourceLanguage.  This may change for future engines as a means of transfer learning.
    /// * **targetLanguage**: The target language code (See documentation on endpoint /translation/engines/ - "Create a new translation engine" for details on language codes).
    /// * **SourceFiles**: The source files associated with the corpus
    ///   * **FileId**: The unique id referencing the uploaded file
    ///   * **TextId**: The client-defined name to associate source and target files.
    ///     * If the TextIds in the SourceFiles and TargetFiles match, they will be used to train the engine.
    ///     * If selected for pretranslation when building, all SourceFiles that have no TargetFile, or lines of text in a SourceFile that have missing or blank lines in the TargetFile will be pretranslated.
    ///     * If a TextId is used more than once in SourceFiles, the sources will be randomly and evenly mixed for training.
    ///     * For pretranslating, multiple sources with the same TextId will be combined, but the first source will always take precedence (no random mixing).
    ///     * For Paratext projects, TextId will be ignored - multiple Paratext source projects will always be mixed (as if they have the same TextId).
    /// * **TargetFiles**: The target files associated with the corpus
    ///   * Same as SourceFiles, except only a single instance of a TextID or a single paratext project is supported.  There is no mixing or combining of multiple targets.
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
    /// Update a corpus with a new set of files
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
    /// Get all corpora for a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The files</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine</response>
    /// <response code="404">The engine does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
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
    /// Get the configuration of a corpus for a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus configuration</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}", Name = "GetTranslationCorpus")]
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
    /// Remove a corpus from a translation engine
    /// </summary>
    /// <remarks>
    /// Removing a corpus will remove all pretranslations associated with that corpus.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="deleteFiles">If true, all files associated with the corpus will be deleted as well (even if they are associated with other corpora). If false, no files will be deleted.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was deleted successfully.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
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
    /// Get all pretranslations in a corpus of a translation engine
    /// </summary>
    /// <remarks>
    /// Pretranslations are arranged in a list of dictionaries with the following fields per pretranslation:
    /// * **TextId**: The TextId of the SourceFile defined when the corpus was created.
    /// * **Refs** (a list of strings): A list of references including:
    ///   * The references defined in the SourceFile per line, if any.
    ///   * An auto-generated reference of `[TextId]:[lineNumber]`, 1 indexed.
    /// * **Translation**: the text of the pretranslation
    ///
    /// Pretranslations can be filtered by text id if provided.
    /// Only pretranslations for the most recent successful build of the engine are returned.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="textId">The text id (optional)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The pretranslations</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}/pretranslations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<PretranslationDto>>> GetAllPretranslationsAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromQuery] string? textId,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.Corpora.Any(c => c.Id == corpusId))
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
        return Ok(pretranslations.Select(Map));
    }

    /// <summary>
    /// Get all pretranslations for the specified text in a corpus of a translation engine
    /// </summary>
    /// <remarks>
    /// Pretranslations are arranged in a list of dictionaries with the following fields per pretranslation:
    /// * **TextId**: The TextId of the SourceFile defined when the corpus was created.
    /// * **Refs** (a list of strings): A list of references including:
    ///   * The references defined in the SourceFile per line, if any.
    ///   * An auto-generated reference of `[TextId]:[lineNumber]`, 1 indexed.
    /// * **Translation**: the text of the pretranslation
    ///
    /// Only pretranslations for the most recent successful build of the engine are returned.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="textId">The text id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The pretranslations</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}/pretranslations/{textId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<PretranslationDto>>> GetPretranslationsByTextIdAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [NotNull] string textId,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.Corpora.Any(c => c.Id == corpusId))
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
        return Ok(pretranslations.Select(Map));
    }

    /// <summary>
    /// Get a pretranslated Scripture book in USFM format.
    /// </summary>
    /// <remarks>
    /// The text that populates the USFM structure can be controlled by the `textOrigin` parameter:
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
    /// Only pretranslations for the most recent successful build of the engine are returned.
    /// Both scripture and non-scripture text in the USFM is parsed and grouped according to [this wiki](https://github.com/sillsdev/serval/wiki/USFM-Parsing-and-Translation).
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="textId">The text id</param>
    /// <param name="textOrigin">The source[s] of the data to populate the USFM file with.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The book in USFM format</response>
    /// <response code="204">The specified book does not exist in the source or target corpus.</response>
    /// <response code="400">The corpus is not a valid Scripture corpus.</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
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
    public async Task<IActionResult> GetPretranslatedUsfmAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [NotNull] string textId,
        [FromQuery(Name = "text-origin")] PretranslationUsfmTextOrigin? textOrigin,
        [FromQuery] PretranslationUsfmTemplate? template,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.Corpora.Any(c => c.Id == corpusId))
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
            cancellationToken
        );
        if (usfm == "")
            return NoContent();
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
    /// Note: Within the returned build, percentCompleted is a value between 0 and 1.
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
    [HttpGet("{id}/builds/{buildId}", Name = "GetTranslationBuild")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationBuildDto>> GetBuildAsync(
        [NotNull] string id,
        [NotNull] string buildId,
        [FromQuery] long? minRevision,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        if (minRevision != null)
        {
            EntityChange<Build> change = await TaskEx.Timeout(
                ct => _buildService.GetNewerRevisionAsync(buildId, minRevision.Value, ct),
                _apiOptions.CurrentValue.LongPollTimeout,
                cancellationToken
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
    /// Specify the corpora and textIds to train on. If no "trainOn" field is provided, all corpora will be used.
    /// Paratext Projects, you may flag a subset of books for training by including their [abbreviations]
    /// Paratext projects can be filtered by [book](https://github.com/sillsdev/libpalaso/blob/master/SIL.Scripture/Canon.cs) using the textId for training.
    /// Filters can also be supplied via scriptureRange parameter as ranges of biblical text. See [here](https://github.com/sillsdev/serval/wiki/Filtering-Paratext-Project-Data-with-a-Scripture-Range)
    /// All Paratext project filtering follows original versification. See [here](https://github.com/sillsdev/serval/wiki/Versification-in-Serval) for more information.
    ///
    /// Specify the corpora or textIds to pretranslate.  When a corpus or textId is selected for pretranslation,
    /// the following text will be pretranslated:
    /// * Text segments that are in the source and not the target (untranslated)
    /// * Text segments that are in the source and the target, but where that target segment is not trained on.
    /// If the engine does not support pretranslation, these fields have no effect.
    /// Pretranslating has the same filtering as training.
    ///
    /// The `"options"` parameter of the build config provides the ability to pass build configuration parameters as a JSON object.
    /// See [nmt job settings documentation](https://github.com/sillsdev/serval/wiki/NMT-Build-Options) about configuring job parameters.
    /// See [keyterms parsing documentation](https://github.com/sillsdev/serval/wiki/Paratext-Key-Terms-Parsing) on how to use keyterms for training.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="buildConfig">The build config (see remarks)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new build job</response>
    /// <response code="400">The build configuration was invalid.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="409">There is already an active or pending build or a build in the process of being canceled.</response>
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
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        Build build = Map(engine, buildConfig);
        await _engineService.StartBuildAsync(build, cancellationToken);

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
        [FromQuery] long? minRevision,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        if (minRevision != null)
        {
            EntityChange<Build> change = await TaskEx.Timeout(
                ct => _buildService.GetActiveNewerRevisionAsync(id, minRevision.Value, ct),
                _apiOptions.CurrentValue.LongPollTimeout,
                cancellationToken
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
    public async Task<ActionResult> CancelBuildAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(id, cancellationToken);
        if (!await _engineService.CancelBuildAsync(id, cancellationToken))
            return NoContent();
        return Ok();
    }

    /// <summary>
    /// Let a link to download the NMT translation model of the last build that was successfully saved.
    /// </summary>
    /// <remarks>
    /// If a Nmt build was successful and IsModelPersisted is `true` for the engine,
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
        ModelDownloadUrl modelInfo = await _engineService.GetModelDownloadUrlAsync(id, cancellationToken);
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

    private static Build Map(Engine engine, TranslationBuildConfigDto source)
    {
        return new Build
        {
            EngineRef = engine.Id,
            Name = source.Name,
            Pretranslate = Map(engine, source.Pretranslate),
            TrainOn = Map(engine, source.TrainOn),
            Options = Map(source.Options)
        };
    }

    private static List<PretranslateCorpus>? Map(Engine engine, IReadOnlyList<PretranslateCorpusConfigDto>? source)
    {
        if (source is null)
            return null;

        var corpusIds = new HashSet<string>(engine.Corpora.Select(c => c.Id));
        var pretranslateCorpora = new List<PretranslateCorpus>();
        foreach (PretranslateCorpusConfigDto ptcc in source)
        {
            if (!corpusIds.Contains(ptcc.CorpusId))
            {
                throw new InvalidOperationException(
                    $"The corpus {ptcc.CorpusId} is not valid: This corpus does not exist for engine {engine.Id}."
                );
            }
            if (ptcc.TextIds != null && ptcc.ScriptureRange != null)
            {
                throw new InvalidOperationException(
                    $"The corpus {ptcc.CorpusId} is not valid: Set at most one of TextIds and ScriptureRange."
                );
            }
            pretranslateCorpora.Add(
                new PretranslateCorpus
                {
                    CorpusRef = ptcc.CorpusId,
                    TextIds = ptcc.TextIds?.ToList(),
                    ScriptureRange = ptcc.ScriptureRange
                }
            );
        }
        return pretranslateCorpora;
    }

    private static List<TrainingCorpus>? Map(Engine engine, IReadOnlyList<TrainingCorpusConfigDto>? source)
    {
        if (source is null)
            return null;

        var corpusIds = new HashSet<string>(engine.Corpora.Select(c => c.Id));
        var trainOnCorpora = new List<TrainingCorpus>();
        foreach (TrainingCorpusConfigDto tcc in source)
        {
            if (!corpusIds.Contains(tcc.CorpusId))
            {
                throw new InvalidOperationException(
                    $"The corpus {tcc.CorpusId} is not valid: This corpus does not exist for engine {engine.Id}."
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
        return trainOnCorpora;
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
            Url = _urlService.GetUrl("GetTranslationEngine", new { id = source.Id }),
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
            Url = _urlService.GetUrl("GetTranslationBuild", new { id = source.EngineRef, buildId = source.Id }),
            Revision = source.Revision,
            Name = source.Name,
            Engine = new ResourceLinkDto
            {
                Id = source.EngineRef,
                Url = _urlService.GetUrl("GetTranslationEngine", new { id = source.EngineRef })
            },
            TrainOn = source.TrainOn?.Select(s => Map(source.EngineRef, s)).ToList(),
            Pretranslate = source.Pretranslate?.Select(s => Map(source.EngineRef, s)).ToList(),
            Step = source.Step,
            PercentCompleted = source.PercentCompleted,
            Message = source.Message,
            QueueDepth = source.QueueDepth,
            State = source.State,
            DateFinished = source.DateFinished,
            Options = source.Options
        };
    }

    private PretranslateCorpusDto Map(string engineId, PretranslateCorpus source)
    {
        return new PretranslateCorpusDto
        {
            Corpus = new ResourceLinkDto
            {
                Id = source.CorpusRef,
                Url = _urlService.GetUrl("GetTranslationCorpus", new { id = engineId, corpusId = source.CorpusRef })
            },
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange
        };
    }

    private TrainingCorpusDto Map(string engineId, TrainingCorpus source)
    {
        return new TrainingCorpusDto
        {
            Corpus = new ResourceLinkDto
            {
                Id = source.CorpusRef,
                Url = _urlService.GetUrl("GetTranslationCorpus", new { id = engineId, corpusId = source.CorpusRef })
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
            Url = _urlService.GetUrl("GetTranslationCorpus", new { id = engineId, corpusId = source.Id }),
            Engine = new ResourceLinkDto
            {
                Id = engineId,
                Url = _urlService.GetUrl("GetTranslationEngine", new { id = engineId })
            },
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = source.SourceFiles.Select(Map).ToList(),
            TargetFiles = source.TargetFiles.Select(Map).ToList()
        };
    }

    private TranslationCorpusFileDto Map(CorpusFile source)
    {
        return new TranslationCorpusFileDto
        {
            File = new ResourceLinkDto
            {
                Id = source.Id,
                Url = _urlService.GetUrl("GetDataFile", new { id = source.Id })
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
}
