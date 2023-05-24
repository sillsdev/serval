using Amazon.Auth.AccessControlPolicy;

namespace Serval.Translation.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/engines")]
[OpenApiTag("Translation Engines")]
public class TranslationEnginesController : ServalControllerBase
{
    private readonly IEngineService _engineService;
    private readonly IBuildService _buildService;
    private readonly IPretranslationService _pretranslationService;
    private readonly IOptionsMonitor<ApiOptions> _apiOptions;
    private readonly IUrlService _urlService;

    public TranslationEnginesController(
        IAuthorizationService authService,
        IEngineService engineService,
        IBuildService buildService,
        IPretranslationService pretranslationService,
        IOptionsMonitor<ApiOptions> apiOptions,
        IUrlService urlService
    )
        : base(authService)
    {
        _engineService = engineService;
        _buildService = buildService;
        _pretranslationService = pretranslationService;
        _apiOptions = apiOptions;
        _urlService = urlService;
    }

    /// <summary>
    /// Gets all translation engines.
    /// </summary>
    /// <response code="200">The engines.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet]
    public async Task<IEnumerable<TranslationEngineDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _engineService.GetAllAsync(Owner, cancellationToken)).Select(Map);
    }

    /// <summary>
    /// Gets a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The translation engine.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}", Name = "GetTranslationEngine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TranslationEngineDto>> GetAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Engine? engine = await _engineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        return Ok(Map(engine));
    }

    /// <summary>
    /// Creates a new translation engine.
    /// </summary>
    /// <param name="engineConfig">The translation engine configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="201">The translation engine was created successfully.</response>
    [Authorize(Scopes.CreateTranslationEngines)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<TranslationEngineDto>> CreateAsync(
        [FromBody] TranslationEngineConfigDto engineConfig,
        CancellationToken cancellationToken
    )
    {
        Engine engine = Map(engineConfig);
        await _engineService.CreateAsync(engine, cancellationToken);
        TranslationEngineDto dto = Map(engine);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Deletes a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The engine was successfully deleted.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.DeleteTranslationEngines)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        if (!await _engineService.DeleteAsync(id, cancellationToken))
            return NotFound();
        return Ok();
    }

    /// <summary>
    /// Translates a segment of text.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="segment">The source segment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The translation result.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="405">The method is not supported.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpPost("{id}/translate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    public async Task<ActionResult<TranslationResultDto>> TranslateAsync(
        [NotNull] string id,
        [FromBody] string segment,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        TranslationResult? result = await _engineService.TranslateAsync(id, segment, cancellationToken);
        if (result == null)
            return NotFound();
        return Ok(Map(result));
    }

    /// <summary>
    /// Translates a segment of text into the top N results.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="n">The number of translations.</param>
    /// <param name="segment">The source segment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The translation results.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="405">The method is not supported.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpPost("{id}/translate/{n}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    public async Task<ActionResult<IEnumerable<TranslationResultDto>>> TranslateNAsync(
        [NotNull] string id,
        [NotNull] int n,
        [FromBody] string segment,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        IEnumerable<TranslationResult>? results = await _engineService.TranslateAsync(
            id,
            n,
            segment,
            cancellationToken
        );
        if (results == null)
            return NotFound();
        return Ok(results.Select(Map));
    }

    /// <summary>
    /// Gets the word graph that represents all possible translations of a segment of text.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="segment">The source segment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The word graph.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="405">The translation engine does not support producing a word graph.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpPost("{id}/get-word-graph")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    public async Task<ActionResult<WordGraphDto>> GetWordGraphAsync(
        [NotNull] string id,
        [FromBody] string segment,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        WordGraph? wordGraph = await _engineService.GetWordGraphAsync(id, segment, cancellationToken);
        if (wordGraph == null)
            return NotFound();
        return Ok(Map(wordGraph));
    }

    /// <summary>
    /// Incrementally trains a translation engine with a segment pair.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="segmentPair">The segment pair.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The engine was trained successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="405">The translation engine does not support incremental training.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/train-segment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    public async Task<ActionResult> TrainSegmentAsync(
        [NotNull] string id,
        [FromBody] SegmentPairDto segmentPair,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

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
            return NotFound();
        }
        return Ok();
    }

    /// <summary>
    /// Adds a corpus to a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="corpusConfig">The corpus configuration.</param>
    /// <param name="getDataFileClient">The data file client.</param>
    /// <param name="idGenerator">The id generator.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="201">The corpus was added successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/corpora")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TranslationCorpusDto>> AddCorpusAsync(
        [NotNull] string id,
        [FromBody] TranslationCorpusConfigDto corpusConfig,
        [FromServices] IRequestClient<GetDataFile> getDataFileClient,
        [FromServices] IIdGenerator idGenerator,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        Corpus corpus;
        try
        {
            corpus = await MapAsync(getDataFileClient, idGenerator.GenerateId(), corpusConfig, cancellationToken);
        }
        catch (InvalidOperationException ioe)
        {
            return UnprocessableEntity(ioe.Message);
        }

        await _engineService.AddCorpusAsync(id, corpus, cancellationToken);
        TranslationCorpusDto dto = Map(id, corpus);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Updates a corpus.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="corpusId">The corpus id.</param>
    /// <param name="corpusConfig">The corpus configuration.</param>
    /// <param name="getDataFileClient">The data file client.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The corpus was updated successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPatch("{id}/corpora/{corpusId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TranslationCorpusDto>> UpdateCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromBody] TranslationCorpusUpdateConfigDto corpusConfig,
        [FromServices] IRequestClient<GetDataFile> getDataFileClient,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        Corpus? corpus = await _engineService.UpdateCorpusAsync(
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
        if (corpus is null)
            return NotFound();
        return Ok(Map(id, corpus));
    }

    /// <summary>
    /// Gets all corpora for a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The files.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TranslationCorpusDto>>> GetAllCorporaAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Engine? engine = await _engineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        return Ok(engine.Corpora.Select(c => Map(id, c)));
    }

    /// <summary>
    /// Gets the configuration of a corpus for a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="corpusId">The corpus id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The corpus configuration.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}", Name = "GetTranslationCorpus")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TranslationCorpusDto>> GetCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        CancellationToken cancellationToken
    )
    {
        Engine? engine = await _engineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        Corpus? corpus = engine.Corpora.FirstOrDefault(f => f.Id == corpusId);
        if (corpus == null)
            return NotFound();

        return Ok(Map(id, corpus));
    }

    /// <summary>
    /// Removes a corpus from a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="corpusId">The corpus id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The data file was deleted successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpDelete("{id}/corpora/{corpusId}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        if (!await _engineService.DeleteCorpusAsync(id, corpusId, cancellationToken))
            return NotFound();

        return Ok();
    }

    /// <summary>
    /// Gets all pretranslations in a corpus of a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="corpusId">The corpus id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The pretranslations.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}/pretranslations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<PretranslationDto>>> GetAllPretranslationsAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        return Ok((await _pretranslationService.GetAllAsync(id, corpusId, cancellationToken)).Select(Map));
    }

    /// <summary>
    /// Gets all pretranslations in a corpus text of a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="corpusId">The corpus id.</param>
    /// <param name="textId">The text id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The pretranslations.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/corpora/{corpusId}/pretranslations/{textId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<PretranslationDto>>> GetAllPretranslationsAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [NotNull] string textId,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? result))
            return result;

        return Ok((await _pretranslationService.GetAllAsync(id, corpusId, textId, cancellationToken)).Select(Map));
    }

    /// <summary>
    /// Gets all build jobs for a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The build jobs.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TranslationBuildDto>>> GetAllBuildsAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        return Ok((await _buildService.GetAllAsync(id, cancellationToken)).Select(Map));
    }

    /// <summary>
    /// Gets a build job.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="buildId">The build job id.</param>
    /// <param name="minRevision">The minimum revision.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The build job.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="404">The build does not exist.</response>
    /// <response code="408">The long polling request timed out.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/builds/{buildId}", Name = "GetTranslationBuild")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    public async Task<ActionResult<TranslationBuildDto>> GetBuildAsync(
        [NotNull] string id,
        [NotNull] string buildId,
        [FromQuery] long? minRevision,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

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
            Build? build = await _buildService.GetAsync(buildId, cancellationToken);
            if (build == null)
                return NotFound();

            return Ok(Map(build));
        }
    }

    /// <summary>
    /// Starts a build job for a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="buildConfig">
    /// Specify the corpora or textId's to pretranslate.  Only "untranslated" text will be pretranslated, that is, segments (lines of text) in the specified corpora or textId's that have untranslated text but no translated text.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="201">The build job was started successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TranslationBuildDto>> StartBuildAsync(
        [NotNull] string id,
        [FromBody] TranslationBuildConfigDto buildConfig,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        Build build = Map(id, buildConfig);
        if (!await _engineService.StartBuildAsync(build, cancellationToken))
            return NotFound();
        var dto = Map(build);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Gets the currently running build job for a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="minRevision">The minimum revision.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The build job.</response>
    /// <response code="204">There is no build currently running.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="408">The long polling request timed out.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/current-build")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    public async Task<ActionResult<TranslationBuildDto>> GetCurrentBuildAsync(
        [NotNull] string id,
        [FromQuery] long? minRevision,
        CancellationToken cancellationToken
    )
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

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
    /// Cancels the current build job for a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The build job was cancelled successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="405">The translation engine does not support cancelling builds.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/current-build/cancel")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    public async Task<ActionResult> CancelBuildAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        if (!(await AuthorizeAsync(id, cancellationToken)).IsSuccess(out ActionResult? errorResult))
            return errorResult;

        await _engineService.CancelBuildAsync(id, cancellationToken);
        return Ok();
    }

    private async Task<(bool, ActionResult?)> AuthorizeAsync(string id, CancellationToken cancellationToken)
    {
        Engine? engine = await _engineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return (false, NotFound());
        if (!await AuthorizeIsOwnerAsync(engine))
            return (false, Forbid());
        return (true, null);
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

    private async Task<IList<CorpusFile>> MapAsync(
        IRequestClient<GetDataFile> getDataFileClient,
        IEnumerable<TranslationCorpusFileConfigDto> fileConfigs,
        CancellationToken cancellationToken
    )
    {
        var files = new List<CorpusFile>();
        foreach (TranslationCorpusFileConfigDto fileConfig in fileConfigs)
        {
            var response = await getDataFileClient.GetResponse<DataFileResult, DataFileNotFound>(
                new GetDataFile { DataFileId = fileConfig.FileId, Owner = Owner },
                cancellationToken
            );
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
            Type = source.Type,
            Owner = Owner
        };
    }

    private static Build Map(string engineId, TranslationBuildConfigDto source)
    {
        return new Build
        {
            EngineRef = engineId,
            Pretranslate = source.Pretranslate
                ?.Select(c => new PretranslateCorpus { CorpusRef = c.CorpusId, TextIds = c.TextIds?.ToList() })
                .ToList()
        };
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
            Type = source.Type,
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
            Engine = new ResourceLinkDto
            {
                Id = source.EngineRef,
                Url = _urlService.GetUrl("GetTranslationEngine", new { id = source.EngineRef })
            },
            Pretranslate = source.Pretranslate?.Select(s => Map(source.EngineRef, s)).ToList(),
            Step = source.Step,
            PercentCompleted = source.PercentCompleted,
            Message = source.Message,
            State = source.State,
            DateFinished = source.DateFinished
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
            TextIds = source.TextIds
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
}
