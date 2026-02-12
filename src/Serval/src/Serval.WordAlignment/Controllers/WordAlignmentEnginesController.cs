namespace Serval.WordAlignment.Controllers;

#pragma warning disable CS0612 // Type or member is obsolete

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/word-alignment/engines")]
[OpenApiTag("Word Alignment Engines")]
public class WordAlignmentEnginesController(
    IAuthorizationService authService,
    IEngineService engineService,
    IBuildService buildService,
    IWordAlignmentService wordAlignmentService,
    IOptionsMonitor<ApiOptions> apiOptions,
    IConfiguration configuration,
    IUrlService urlService,
    ILogger<WordAlignmentEnginesController> logger
) : ServalControllerBase(authService)
{
    private static readonly JsonSerializerOptions ObjectJsonSerializerOptions = new()
    {
        Converters = { new ObjectToInferredTypesConverter() },
    };

    private readonly IEngineService _engineService = engineService;
    private readonly IBuildService _buildService = buildService;
    private readonly IWordAlignmentService _wordAlignmentService = wordAlignmentService;
    private readonly IOptionsMonitor<ApiOptions> _apiOptions = apiOptions;
    private readonly IUrlService _urlService = urlService;
    private readonly ILogger<WordAlignmentEnginesController> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

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
    /// * **`name`**: (optional) A name to help identify and distinguish the file.
    ///   * Recommendation: Create a multi-part name to distinguish between projects, uses, etc.
    ///   * The name does not have to be unique, as the engine is uniquely identified by the auto-generated id
    /// * **`sourceLanguage`**: The source language code (a valid [IETF language tag](https://en.wikipedia.org/wiki/IETF_language_tag) is recommended)
    /// * **`targetLanguage`**: The target language code (a valid IETF language tag is recommended)
    /// * **type**: **`statistical`** or **`echo-word-alignment`**
    /// ### statistical
    /// The Statistical engine is based off of the [Thot library](https://github.com/sillsdev/thot) and contains IBM-1, IBM-2, IBM-3, IBM-4, FastAlign and HMM algorithms.
    /// ### echo-word-alignment
    /// The echo-word-alignment engine has full coverage of all endpoints. Endpoints like create and build return empty responses.
    /// Endpoints like align echo the sent content back to the user in the proper format. This engine is useful for debugging and testing purposes.
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
    /// Align words between a source and target segment
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="wordAlignmentRequest">The source and target segment</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The word alignment result</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The method is not supported.</response>
    /// <response code="409">The engine needs to be built before it can align segments.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpPost("{id}/align")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentResultDto>> AlignAsync(
        [NotNull] string id,
        [FromBody] WordAlignmentRequestDto wordAlignmentRequest,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        WordAlignmentResult? result = await _engineService.GetWordAlignmentAsync(
            id,
            wordAlignmentRequest.SourceSegment,
            wordAlignmentRequest.TargetSegment,
            cancellationToken
        );
        if (result is null)
            return Conflict();
        _logger.LogInformation("Got word alignment for engine {EngineId}", id);
        return Ok(Map(result));
    }

    /// <summary>
    /// Add a parallel corpus to an engine
    /// </summary>
    /// <remarks>
    /// ## Parameters
    /// * **`sourceCorpusIds`**: The source corpora associated with the parallel corpus
    /// * **`targetCorpusIds`**: The target corpora associated with the parallel corpus
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="corpusConfig">The corpus configuration (see remarks)</param>
    /// <param name="getCorpusClient"></param>
    /// <param name="idGenerator"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The added corpus</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateWordAlignmentEngines)]
    [HttpPost("{id}/parallel-corpora")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentParallelCorpusDto>> AddParallelCorpusAsync(
        [NotNull] string id,
        [FromBody] WordAlignmentParallelCorpusConfigDto corpusConfig,
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
        WordAlignmentParallelCorpusDto dto = Map(id, corpus);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Update a parallel corpus with a new set of corpora
    /// </summary>
    /// <remarks>
    /// Will completely replace the parallel corpus' file associations. Will not affect jobs already queued or running. Will not affect existing word graphs until new build is complete.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="corpusConfig">The corpus configuration</param>
    /// <param name="getCorpusClient">The data file client</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was updated successfully</response>
    /// <response code="400">Bad request</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateWordAlignmentEngines)]
    [HttpPatch("{id}/parallel-corpora/{parallelCorpusId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentParallelCorpusDto>> UpdateParallelCorpusAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        [FromBody] WordAlignmentParallelCorpusUpdateConfigDto corpusConfig,
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
    /// Get all parallel corpora for a engine
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The parallel corpora</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine</response>
    /// <response code="404">The engine does not exist</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}/parallel-corpora")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<WordAlignmentParallelCorpusDto>>> GetAllParallelCorporaAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        return Ok(engine.ParallelCorpora.Select(c => Map(id, c)));
    }

    /// <summary>
    /// Get the configuration of a parallel corpus for a engine
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The parallel corpus configuration</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadWordAlignmentEngines)]
    [HttpGet("{id}/parallel-corpora/{parallelCorpusId}", Name = Endpoints.GetParallelWordAlignmentCorpus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentParallelCorpusDto>> GetParallelCorpusAsync(
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
    /// Remove a parallel corpus from a engine
    /// </summary>
    /// <remarks>
    /// Removing a parallel corpus will remove all word alignments associated with that corpus.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The parallel corpus was deleted successfully.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateWordAlignmentEngines)]
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
        [FromQuery(Name = "textId")] string? textIdCamelCase,
        CancellationToken cancellationToken
    )
    {
        textId ??= textIdCamelCase;
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        if (!engine.ParallelCorpora.Any(c => c.Id == corpusId))
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
    /// Note: Within the returned build, progress is a value between 0 and 1.
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
        [FromQuery(Name = "min-revision")] long? minRevision,
        [FromQuery(Name = "minRevision")] long? minRevisionCamelCase,
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
    /// Starts a build job for a engine.
    /// </summary>
    /// <remarks>
    /// Specify the corpora and textIds to train on. If no `trainOn` field is provided, all corpora will be used. Only parallel corpora are supported.
    /// Paratext projects can be filtered by [book using the `textIds`](https://github.com/sillsdev/libpalaso/blob/master/SIL.Scripture/Canon.cs).
    /// Filters can also be supplied via `scriptureRange` parameter as ranges of biblical text. See [here](https://github.com/sillsdev/serval/wiki/Filtering-Paratext-Project-Data-with-a-Scripture-Range)
    /// All Paratext project filtering follows original versification. See [here](https://github.com/sillsdev/serval/wiki/Versification-in-Serval) for more information.
    ///
    /// Specify the corpora or text ids to word align on.
    /// When a corpus or text id is selected for word align on, only text segments that are in both the source and the target will be aligned.
    ///
    /// The `options` parameter of the build config provides the ability to pass build configuration parameters as a JSON object.
    /// See [statistical alignment job settings documentation](https://github.com/sillsdev/serval/wiki/Statistical-Alignment-Build-Options) about configuring job parameters.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="buildConfig">The build config (see remarks)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new build job</response>
    /// <response code="400">The build configuration was invalid.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="409">There is already an active/pending build or a build in the process of being canceled.</response>
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
        string deploymentVersion = _configuration.GetValue<string>("deploymentVersion") ?? "Unknown";

        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        Build build = Map(engine, buildConfig, deploymentVersion);
        if (!await _engineService.StartBuildAsync(build, cancellationToken))
            return Conflict();

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
        [FromQuery(Name = "min-revision")] long? minRevision,
        [FromQuery(Name = "minRevision")] long? minRevisionCamelCase,
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
    public async Task<ActionResult<WordAlignmentBuildDto>> CancelBuildAsync(
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

    private async Task AuthorizeAsync(string id, CancellationToken cancellationToken)
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
    }

    private async Task<ParallelCorpus> MapAsync(
        IRequestClient<GetCorpus> getDataFileClient,
        string corpusId,
        WordAlignmentParallelCorpusConfigDto source,
        CancellationToken cancellationToken
    )
    {
        return new ParallelCorpus
        {
            Id = corpusId,
            SourceCorpora = await MapAsync(getDataFileClient, source.SourceCorpusIds, cancellationToken),
            TargetCorpora = await MapAsync(getDataFileClient, source.TargetCorpusIds, cancellationToken),
        };
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
                                TextId = f.TextId,
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

    private WordAlignmentParallelCorpusDto Map(string engineId, ParallelCorpus source)
    {
        return new WordAlignmentParallelCorpusDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl(Endpoints.GetCorpus, new { id = engineId, corpusId = source.Id }),
            Engine = new ResourceLinkDto
            {
                Id = engineId,
                Url = _urlService.GetUrl(Endpoints.GetWordAlignmentEngine, new { id = engineId }),
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

    private static Build Map(Engine engine, WordAlignmentBuildConfigDto source, string deploymentVersion)
    {
        return new Build
        {
            EngineRef = engine.Id,
            Name = source.Name,
            WordAlignOn = Map(engine, source.WordAlignOn),
            TrainOn = Map(engine, source.TrainOn),
            Options = Map(source.Options),
            DeploymentVersion = deploymentVersion,
        };
    }

    private static List<WordAlignmentCorpus>? Map(Engine engine, IReadOnlyList<WordAlignmentCorpusConfigDto>? source)
    {
        if (source is null)
            return null;

        if (source.Select(p => p.ParallelCorpusId).Distinct().Count() != source.Count)
            throw new InvalidOperationException("Each ParallelCorpusId may only be specified once.");

        var corpusIds = new HashSet<string>(engine.ParallelCorpora.Select(c => c.Id));
        var wordAlignmentCorpora = new List<WordAlignmentCorpus>();
        foreach (WordAlignmentCorpusConfigDto cc in source)
        {
            if (cc.ParallelCorpusId == null)
            {
                throw new InvalidOperationException($"ParallelCorpusId must be set.");
            }
            if (!corpusIds.Contains(cc.ParallelCorpusId))
            {
                throw new InvalidOperationException(
                    $"The parallel corpus {cc.ParallelCorpusId} is not valid: This parallel corpus does not exist for engine {engine.Id}."
                );
            }
            ParallelCorpus corpus = engine.ParallelCorpora.Single(pc => pc.Id == cc.ParallelCorpusId);
            if (corpus.SourceCorpora.Count == 0 && corpus.TargetCorpora.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The corpus {cc.ParallelCorpusId} does not have source or target corpora associated with it."
                );
            }
            if (
                cc.SourceFilters != null
                && cc.SourceFilters.Count > 0
                && (
                    cc.SourceFilters.Select(sf => sf.CorpusId).Distinct().Count() > 1
                    || cc.SourceFilters[0].CorpusId
                        != engine.ParallelCorpora.Single(pc => pc.Id == cc.ParallelCorpusId).SourceCorpora[0].Id
                )
            )
            {
                throw new InvalidOperationException(
                    $"Only the first source corpus in a parallel corpus may be filtered for alignment."
                );
            }
            wordAlignmentCorpora.Add(
                new WordAlignmentCorpus
                {
                    ParallelCorpusRef = cc.ParallelCorpusId,
                    SourceFilters = cc.SourceFilters?.Select(Map).ToList(),
                    TargetFilters = cc.TargetFilters?.Select(Map).ToList(),
                }
            );
        }
        return wordAlignmentCorpora;
    }

    private static List<TrainingCorpus>? Map(Engine engine, IReadOnlyList<TrainingCorpusConfigDto>? source)
    {
        if (source is null)
            return null;

        if (source.Select(p => p.ParallelCorpusId).Distinct().Count() != source.Count)
            throw new InvalidOperationException($"Each ParallelCorpusId may only be specified once.");

        var corpusIds = new HashSet<string>(engine.ParallelCorpora.Select(c => c.Id));
        var trainingCorpora = new List<TrainingCorpus>();
        foreach (TrainingCorpusConfigDto cc in source)
        {
            if (cc.CorpusId != null)
            {
                throw new InvalidOperationException($"CorpusId cannot be set. Only ParallelCorpusId is supported.");
            }
            if (cc.ParallelCorpusId == null)
            {
                throw new InvalidOperationException($"ParallelCorpusId must be set.");
            }
            if (!corpusIds.Contains(cc.ParallelCorpusId))
            {
                throw new InvalidOperationException(
                    $"The parallel corpus {cc.ParallelCorpusId} is not valid: This parallel corpus does not exist for engine {engine.Id}."
                );
            }
            ParallelCorpus corpus = engine.ParallelCorpora.Single(pc => pc.Id == cc.ParallelCorpusId);
            if (corpus.SourceCorpora.Count == 0 && corpus.TargetCorpora.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The corpus {cc.ParallelCorpusId} does not have source or target corpora associated with it."
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
            trainingCorpora.Add(
                new TrainingCorpus
                {
                    ParallelCorpusRef = cc.ParallelCorpusId,
                    SourceFilters = cc.SourceFilters?.Select(Map).ToList(),
                    TargetFilters = cc.TargetFilters?.Select(Map).ToList(),
                }
            );
        }
        return trainingCorpora;
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
            ScriptureRange = source.ScriptureRange,
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
            CorpusSize = source.CorpusSize,
            DateCreated = source.DateCreated,
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
                Url = _urlService.GetUrl(
                    Endpoints.GetWordAlignmentBuild,
                    new { id = source.EngineRef, buildId = source.Id }
                ),
            },
            TrainOn = source.TrainOn?.Select(s => Map(source.EngineRef, s)).ToList(),
            WordAlignOn = source.WordAlignOn?.Select(s => Map(source.EngineRef, s)).ToList(),
            Step = source.Step,
            PercentCompleted = source.Progress,
            Progress = source.Progress,
            Message = source.Message,
            QueueDepth = source.QueueDepth,
            State = source.State,
            DateCreated = source.DateCreated,
            DateStarted = source.DateStarted,
            DateCompleted = source.DateCompleted,
            DateFinished = source.DateFinished,
            Options = source.Options,
            DeploymentVersion = source.DeploymentVersion,
            ExecutionData = Map(source.ExecutionData),
            Phases = source.Phases?.Select(Map).ToList(),
        };
    }

    private TrainingCorpusDto Map(string engineId, TrainingCorpus source)
    {
        return new TrainingCorpusDto
        {
            ParallelCorpus =
                source.ParallelCorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.ParallelCorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetParallelWordAlignmentCorpus,
                            new { id = engineId, parallelCorpusId = source.ParallelCorpusRef }
                        ),
                    }
                    : null,
            SourceFilters = source.SourceFilters?.Select(Map).ToList(),
            TargetFilters = source.TargetFilters?.Select(Map).ToList(),
        };
    }

    private WordAlignmentCorpusDto Map(string engineId, WordAlignmentCorpus source)
    {
        return new WordAlignmentCorpusDto
        {
            ParallelCorpus =
                source.ParallelCorpusRef != null
                    ? new ResourceLinkDto
                    {
                        Id = source.ParallelCorpusRef,
                        Url = _urlService.GetUrl(
                            Endpoints.GetParallelWordAlignmentCorpus,
                            new { id = engineId, parallelCorpusId = source.ParallelCorpusRef }
                        ),
                    }
                    : null,
            SourceFilters = source.SourceFilters?.Select(Map).ToList(),
            TargetFilters = source.TargetFilters?.Select(Map).ToList(),
        };
    }

    private ParallelCorpusFilterDto Map(ParallelCorpusFilter source)
    {
        return new ParallelCorpusFilterDto
        {
            Corpus = new ResourceLinkDto
            {
                Id = source.CorpusRef,
                Url = _urlService.GetUrl(Endpoints.GetCorpus, new { id = source.CorpusRef }),
            },
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange,
        };
    }

    private WordAlignmentResultDto Map(WordAlignmentResult source)
    {
        return new WordAlignmentResultDto
        {
            SourceTokens = source.SourceTokens.ToList(),
            TargetTokens = source.TargetTokens.ToList(),
            Alignment = source.Alignment.Select(Map).ToList(),
        };
    }

    private AlignedWordPairDto Map(AlignedWordPair source)
    {
        return new AlignedWordPairDto()
        {
            SourceIndex = source.SourceIndex,
            TargetIndex = source.TargetIndex,
            Score = source.Score,
        };
    }

    private static WordAlignmentDto Map(Models.WordAlignment source)
    {
        return new WordAlignmentDto
        {
            TextId = source.TextId,
            SourceRefs = source.SourceRefs,
            TargetRefs = source.TargetRefs,
            Refs = source.Refs,
            SourceTokens = source.SourceTokens.ToList(),
            TargetTokens = source.TargetTokens.ToList(),
            Alignment = source
                .Alignment.Select(c => new AlignedWordPairDto()
                {
                    SourceIndex = c.SourceIndex,
                    TargetIndex = c.TargetIndex,
                    Score = c.Score,
                })
                .ToList(),
        };
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
            ParallelCorpora = [],
        };
    }

    private static PhaseDto Map(BuildPhase source)
    {
        return new PhaseDto
        {
            Stage = (PhaseStage)source.Stage,
            Step = source.Step,
            StepCount = source.StepCount,
            Started = source.Started,
        };
    }

    private static WordAlignmentExecutionDataDto Map(ExecutionData source)
    {
        return new WordAlignmentExecutionDataDto
        {
            TrainCount = source.TrainCount ?? 0,
            WordAlignCount = source.WordAlignCount ?? 0,
            Warnings = source.Warnings ?? [],
            EngineSourceLanguageTag = source.EngineSourceLanguageTag,
            EngineTargetLanguageTag = source.EngineTargetLanguageTag,
        };
    }
}
