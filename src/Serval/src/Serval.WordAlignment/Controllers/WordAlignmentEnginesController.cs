namespace Serval.WordAlignment.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/word-alignment/engines")]
[OpenApiTag("Word Alignment Engines")]
public class WordAlignmentEnginesController(
    IAuthorizationService authService,
    IEngineService engineService,
    IBuildService buildService,
    IWordAlignmentService wordAlignmentService,
    IOptionsMonitor<ApiOptions> apiOptions,
    IUrlService urlService,
    ILogger<WordAlignmentEnginesController> logger
) : ServalControllerBase(authService)
{
    private static readonly JsonSerializerOptions ObjectJsonSerializerOptions =
        new() { Converters = { new ObjectToInferredTypesConverter() } };

    private readonly IEngineService _engineService = engineService;
    private readonly IBuildService _buildService = buildService;
    private readonly IWordAlignmentService _wordAlignmentService = wordAlignmentService;
    private readonly IOptionsMonitor<ApiOptions> _apiOptions = apiOptions;
    private readonly IUrlService _urlService = urlService;
    private readonly ILogger<WordAlignmentEnginesController> _logger = logger;

    /// <summary>
    /// Get all word alignment engines
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engines</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<WordAlignmentEngineDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _engineService.GetAllAsync(Owner, cancellationToken)).Select(Map);
    }

    /// <summary>
    /// Get a word alignment engine by unique id
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engine</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>

    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}", Name = Endpoints.GetWordAlignmentEngine)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentEngineDto>> GetAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        return Ok(Map(engine));
    }

    /// <summary>
    /// Create a new word alignment engine
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **name**: (optional) A name to help identify and distinguish the file.
    ///   * Recommendation: Create a multi-part name to distinguish between projects, uses, etc.
    ///   * The name does not have to be unique, as the engine is uniquely identified by the auto-generated id
    /// * **sourceLanguage**: The source language code (a valid [IETF language tag](https://en.wikipedia.org/wiki/IETF_language_tag) is recommended)
    /// * **targetLanguage**: The target language code (a valid IETF language tag is recommended)
    /// * **type**: **statistical** or **echo**
    /// ### statistical
    /// The Statistical engine is based off of the [Thot library](https://github.com/sillsdev/thot) and contains IBM-1, IBM-2, IBM-3, IBM-4, FastAlign and HMM algorithms.
    /// ### echo
    /// The echo engine has full coverage of all endpoints. Endpoints like create and build return empty responses.
    /// Endpoints like get-word-alignment echo the sent content back to the user in the proper format. This engine is useful for debugging and testing purposes.
    /// ## Sample request:
    ///
    ///     {
    ///       "name": "myTeam:myProject:myEngine",
    ///       "sourceLanguage": "el",
    ///       "targetLanguage": "en",
    ///       "type": "statistical"
    ///     }
    ///
    /// </remarks>
    /// <param name="engineConfig">The engine configuration (see above)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new engine</response>
    /// <response code="400">Bad request. Is the engine type correct?</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.CreateWordAlignmentEngines)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentEngineDto>> CreateAsync(
        [FromBody] WordAlignmentEngineConfigDto engineConfig,
        CancellationToken cancellationToken
    )
    {
        Engine engine = Map(engineConfig);
        Engine updatedEngine = await _engineService.CreateAsync(engine, cancellationToken);
        WordAlignmentEngineDto dto = Map(updatedEngine);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Delete a word alignment engine
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engine was successfully deleted.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist and therefore cannot be deleted.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.DeleteWordAlignmentEngines)]
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
    /// Align words on a segment of text
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="segment">The source segment</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The word alignment result</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The method is not supported.</response>
    /// <response code="409">The engine needs to be built before it can alignment segments.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpPost("{id}/get-word-alignment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentResultDto>> GetWordAlignmentAsync(
        [NotNull] string id,
        [FromBody] string segment,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        WordAlignmentResult result = await _engineService.GetWordAlignmentAsync(id, segment, cancellationToken);
        _logger.LogInformation("Got word alignment for engine {EngineId}", id);
        return Ok(Map(result));
    }

    /// <summary>
    /// Add a corpus to a engine
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **name**: A name to help identify and distinguish the corpus from other corpora
    ///   * The name does not have to be unique since the corpus is uniquely identified by an auto-generated id
    /// * **sourceLanguage**: The source language code (See documentation on endpoint /word-alignment/engines/ - "Create a new engine" for details on language codes).
    ///   * Normally, this is the same as the engine sourceLanguage.  This may change for future engines as a means of transfer learning.
    /// * **targetLanguage**: The target language code (See documentation on endpoint /word-alignment/engines/ - "Create a new engine" for details on language codes).
    /// * **SourceFiles**: The source files associated with the corpus
    ///   * **FileId**: The unique id referencing the uploaded file
    ///   * **TextId**: The client-defined name to associate source and target files.
    ///     * If the TextIds in the SourceFiles and TargetFiles match, they will be used to train the engine.
    ///     * If a TextId is used more than once in SourceFiles, the sources will be randomly and evenly mixed for training.
    ///     * For Paratext projects, TextId will be ignored - multiple Paratext source projects will always be mixed (as if they have the same TextId).
    /// * **TargetFiles**: The target files associated with the corpus
    ///   * Same as SourceFiles, except only a single instance of a TextID or a single paratext project is supported.  There is no mixing or combining of multiple targets.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="corpusConfig">The corpus configuration (see remarks)</param>
    /// <param name="getDataFileClient"></param>
    /// <param name="idGenerator"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The added corpus</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateWordAlignmentEngines)]
    [HttpPost("{id}/corpora")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentCorpusDto>> AddCorpusAsync(
        [NotNull] string id,
        [FromBody] WordAlignmentCorpusConfigDto corpusConfig,
        [FromServices] IRequestClient<GetDataFile> getDataFileClient,
        [FromServices] IIdGenerator idGenerator,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        Corpus corpus = await MapAsync(getDataFileClient, idGenerator.GenerateId(), corpusConfig, cancellationToken);
        await _engineService.AddCorpusAsync(id, corpus, cancellationToken);
        WordAlignmentCorpusDto dto = Map(id, corpus);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Update a corpus with a new set of files
    /// </summary>
    /// <remarks>
    /// See posting a new corpus for details of use. Will completely replace corpus' file associations.
    /// Will not affect jobs already queued or running. Will not affect existing word alignments until new build is complete.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="corpusConfig">The corpus configuration</param>
    /// <param name="getDataFileClient">The data file client</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was updated successfully</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateWordAlignmentEngines)]
    [HttpPatch("{id}/corpora/{corpusId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentCorpusDto>> UpdateCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromBody] WordAlignmentCorpusUpdateConfigDto corpusConfig,
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
    /// Get all corpora for a engine
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The files</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine</response>
    /// <response code="404">The engine does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}/corpora")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<WordAlignmentCorpusDto>>> GetAllCorporaAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        return Ok(engine.Corpora.Select(c => Map(id, c)));
    }

    /// <summary>
    /// Get the configuration of a corpus for a engine
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus configuration</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}/corpora/{corpusId}", Name = Endpoints.GetWordAlignmentCorpus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentCorpusDto>> GetCorpusAsync(
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
    /// Remove a corpus from a engine
    /// </summary>
    /// <remarks>
    /// Removing a corpus will remove all word alignments associated with that corpus.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="deleteFiles">If true, all files associated with the corpus will be deleted as well (even if they are associated with other corpora). If false, no files will be deleted.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was deleted successfully.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateWordAlignmentEngines)]
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
    /// Get all word alignments in a corpus of a engine
    /// </summary>
    /// <remarks>
    /// Word alignments are arranged in a list of dictionaries with the following fields per word alignment:
    /// * **TextId**: The TextId of the SourceFile defined when the corpus was created.
    /// * **Refs** (a list of strings): A list of references including:
    ///   * The references defined in the SourceFile per line, if any.
    ///   * An auto-generated reference of `[TextId]:[lineNumber]`, 1 indexed.
    /// * **SourceTokens**: the tokenized source segment
    /// * **TargetTokens**: the tokenized target segment
    /// * **Confidences**: the confidence of the alignment ona scale from 0 to 1
    /// * **Alignment**: the word alignment, 0 indexed for source and target positions
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

        IEnumerable<Models.WordAlignment> wordAlignments = await _wordAlignmentService.GetAllAsync(
            id,
            engine.ModelRevision,
            corpusId,
            textId,
            cancellationToken
        );
        _logger.LogInformation(
            "Returning {Count} word alignments for engine {EngineId}, corpus {CorpusId}, and query {TextId}",
            wordAlignments.Count(),
            id,
            corpusId,
            textId
        );
        return Ok(wordAlignments.Select(Map));
    }

    /// <summary>
    /// Get all build jobs for a engine
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build jobs</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<WordAlignmentBuildDto>>> GetAllBuildsAsync(
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
    /// <param name="id">The engine id</param>
    /// <param name="buildId">The build job id</param>
    /// <param name="minRevision">The minimum revision</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build job</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the engine.</response>
    /// <response code="404">The engine or build does not exist.</response>
    /// <response code="408">The long polling request timed out. This is expected behavior if you're using long-polling with the minRevision strategy specified in the docs.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}/builds/{buildId}", Name = Endpoints.GetWordAlignmentBuild)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentBuildDto>> GetBuildAsync(
        [NotNull] string id,
        [NotNull] string buildId,
        [FromQuery] long? minRevision,
        CancellationToken cancellationToken
    )
    {
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
    /// Starts a build job for a engine.
    /// </summary>
    /// <remarks>
    /// Specify the corpora and textIds to train on. If no "trainOn" field is provided, all corpora will be used.
    /// Paratext Projects, you may flag a subset of books for training by including their [abbreviations]
    /// Paratext projects can be filtered by [book](https://github.com/sillsdev/libpalaso/blob/master/SIL.Scripture/Canon.cs) using the textId for training.
    /// Filters can also be supplied via scriptureRange parameter as ranges of biblical text. See [here](https://github.com/sillsdev/serval/wiki/Filtering-Paratext-Project-Data-with-a-Scripture-Range)
    /// All Paratext project filtering follows original versification. See [here](https://github.com/sillsdev/serval/wiki/Versification-in-Serval) for more information.
    ///
    /// Specify the corpora or textIds to word align on.
    /// When a corpus or textId is selected for word align on, only text segments that are in both the source and the target will be aligned.
    ///
    /// The `"options"` parameter of the build config provides the ability to pass build configuration parameters as a JSON object.
    /// See [statistical alignment job settings documentation](https://github.com/sillsdev/serval/wiki/Statistical-Alignment-Build-Options) about configuring job parameters.
    /// See [keyterms parsing documentation](https://github.com/sillsdev/serval/wiki/Paratext-Key-Terms-Parsing) on how to use keyterms for training.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="buildConfig">The build config (see remarks)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new build job</response>
    /// <response code="400">The build configuration was invalid.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="409">There is already an active or pending build or a build in the process of being canceled.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateWordAlignmentEngines)]
    [HttpPost("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentBuildDto>> StartBuildAsync(
        [NotNull] string id,
        [FromBody] WordAlignmentBuildConfigDto buildConfig,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        Build build = Map(engine, buildConfig);
        await _engineService.StartBuildAsync(build, cancellationToken);

        WordAlignmentBuildDto dto = Map(build);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Get the currently running build job for a engine
    /// </summary>
    /// <remarks>
    /// See documentation on endpoint /word-alignment/engines/{id}/builds/{id} - "Get a Build Job" for details on using `minRevision`.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="minRevision">The minimum revision</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build job</response>
    /// <response code="204">There is no build currently running.</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="408">The long polling request timed out. This is expected behavior if you're using long-polling with the minRevision strategy specified in the docs.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}/current-build")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentBuildDto>> GetCurrentBuildAsync(
        [NotNull] string id,
        [FromQuery] long? minRevision,
        CancellationToken cancellationToken
    )
    {
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
    /// Cancel the current build job (whether pending or active) for a engine
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build job was cancelled successfully.</response>
    /// <response code="204">There is no active build job.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The engine does not support cancelling builds.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateWordAlignmentEngines)]
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

    private async Task AuthorizeAsync(string id, CancellationToken cancellationToken)
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
    }

    private async Task<Corpus> MapAsync(
        IRequestClient<GetDataFile> getDataFileClient,
        string corpusId,
        WordAlignmentCorpusConfigDto source,
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
        IEnumerable<WordAlignmentCorpusFileConfigDto> fileConfigs,
        CancellationToken cancellationToken
    )
    {
        var files = new List<CorpusFile>();
        foreach (WordAlignmentCorpusFileConfigDto fileConfig in fileConfigs)
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

    private Engine Map(WordAlignmentEngineConfigDto source)
    {
        return new Engine
        {
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            Type = source.Type.ToPascalCase(),
            Owner = Owner,
            Corpora = [],
        };
    }

    private static Build Map(Engine engine, WordAlignmentBuildConfigDto source)
    {
        return new Build
        {
            EngineRef = engine.Id,
            Name = source.Name,
            WordAlignOn = Map(engine, source.WordAlignOn),
            TrainOn = Map(engine, source.TrainOn),
            Options = Map(source.Options)
        };
    }

    private static List<WordAlignmentCorpus>? Map(Engine engine, IReadOnlyList<WordAlignOnCorpusConfigDto>? source)
    {
        if (source is null)
            return null;

        var corpusIds = new HashSet<string>(engine.Corpora.Select(c => c.Id));
        var wordAlignmentCorpora = new List<WordAlignmentCorpus>();
        foreach (WordAlignOnCorpusConfigDto ptcc in source)
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
            wordAlignmentCorpora.Add(
                new WordAlignmentCorpus
                {
                    CorpusRef = ptcc.CorpusId,
                    TextIds = ptcc.TextIds?.ToList(),
                    ScriptureRange = ptcc.ScriptureRange
                }
            );
        }
        return wordAlignmentCorpora;
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

    private WordAlignmentEngineDto Map(Engine source)
    {
        return new WordAlignmentEngineDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = source.Id }),
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            Type = source.Type.ToKebabCase(),
            IsBuilding = source.IsBuilding,
            ModelRevision = source.ModelRevision,
            Confidence = Math.Round(source.Confidence, 8),
            CorpusSize = source.CorpusSize
        };
    }

    private WordAlignmentBuildDto Map(Build source)
    {
        return new WordAlignmentBuildDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(
                Endpoints.GetWordAlignmentBuild,
                new { id = source.EngineRef, buildId = source.Id }
            ),
            Revision = source.Revision,
            Name = source.Name,
            Engine = new ResourceLinkDto
            {
                Id = source.EngineRef,
                Url = _urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = source.EngineRef })
            },
            TrainOn = source.TrainOn?.Select(s => Map(source.EngineRef, s)).ToList(),
            WordAlignOn = source.WordAlignOn?.Select(s => Map(source.EngineRef, s)).ToList(),
            Step = source.Step,
            PercentCompleted = source.PercentCompleted,
            Message = source.Message,
            QueueDepth = source.QueueDepth,
            State = source.State,
            DateFinished = source.DateFinished,
            Options = source.Options
        };
    }

    private WordAlignOnCorpusDto Map(string engineId, WordAlignmentCorpus source)
    {
        return new WordAlignOnCorpusDto
        {
            Corpus = new ResourceLinkDto
            {
                Id = source.CorpusRef,
                Url = _urlService.GetUrl(
                    Endpoints.GetWordAlignmentCorpus,
                    new { id = engineId, corpusId = source.CorpusRef }
                )
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
                Url = _urlService.GetUrl(
                    Endpoints.GetWordAlignmentCorpus,
                    new { id = engineId, corpusId = source.CorpusRef }
                )
            },
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange
        };
    }

    private WordAlignmentResultDto Map(WordAlignmentResult source)
    {
        return new WordAlignmentResultDto
        {
            SourceTokens = source.SourceTokens.ToList(),
            TargetTokens = source.TargetTokens.ToList(),
            Confidences = source.Confidences.Select(c => Math.Round(c, 8)).ToList(),
            Alignment = source.Alignment.Select(Map).ToList(),
        };
    }

    private AlignedWordPairDto Map(AlignedWordPair source)
    {
        return new AlignedWordPairDto() { SourceIndex = source.SourceIndex, TargetIndex = source.TargetIndex };
    }

    private static WordAlignmentDto Map(Models.WordAlignment source)
    {
        return new WordAlignmentDto
        {
            TextId = source.TextId,
            Refs = source.Refs,
            SourceTokens = source.SourceTokens.ToList(),
            TargetTokens = source.TargetTokens.ToList(),
            Confidences = source.Confidences.Select(c => Math.Round(c, 8)).ToList(),
            Alignment = source
                .Alignment.Select(c => new AlignedWordPairDto()
                {
                    SourceIndex = c.SourceIndex,
                    TargetIndex = c.TargetIndex
                })
                .ToList(),
        };
    }

    private WordAlignmentCorpusDto Map(string engineId, Corpus source)
    {
        return new WordAlignmentCorpusDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(Endpoints.GetWordAlignmentCorpus, new { id = engineId, corpusId = source.Id }),
            Engine = new ResourceLinkDto
            {
                Id = engineId,
                Url = _urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = engineId })
            },
            Name = source.Name,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = source.SourceFiles.Select(Map).ToList(),
            TargetFiles = source.TargetFiles.Select(Map).ToList()
        };
    }

    private WordAlignmentCorpusFileDto Map(CorpusFile source)
    {
        return new WordAlignmentCorpusFileDto
        {
            File = new ResourceLinkDto
            {
                Id = source.Id,
                Url = _urlService.GetUrl(Endpoints.GetDataFile, new { id = source.Id })
            },
            TextId = source.TextId
        };
    }
}
