namespace Serval.Translation.Controllers;

[Route("translation_engines")]
[OpenApiTag("Translation Engines")]
public class TranslationEnginesController : ServalControllerBase
{
    private readonly ITranslationEngineService _translationEngineService;
    private readonly IBuildService _buildService;
    private readonly IPretranslationService _pretranslationService;
    private readonly IOptionsMonitor<ApiOptions> _apiOptions;
    private readonly IMapper _mapper;
    private readonly IDataFileRetriever _dataFileRetriever;

    public TranslationEnginesController(
        IAuthorizationService authService,
        ITranslationEngineService translationEngineService,
        IBuildService buildService,
        IPretranslationService pretranslationService,
        IOptionsMonitor<ApiOptions> apiOptions,
        IMapper mapper,
        IDataFileRetriever dataFileRetriever
    )
        : base(authService)
    {
        _translationEngineService = translationEngineService;
        _buildService = buildService;
        _pretranslationService = pretranslationService;
        _apiOptions = apiOptions;
        _mapper = mapper;
        _dataFileRetriever = dataFileRetriever;
    }

    /// <summary>
    /// Gets all translation engines.
    /// </summary>
    /// <response code="200">The engines.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet]
    public async Task<IEnumerable<TranslationEngineDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _translationEngineService.GetAllAsync(Owner, cancellationToken)).Select(
            _mapper.Map<TranslationEngineDto>
        );
    }

    /// <summary>
    /// Gets a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The translation engine.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TranslationEngineDto>> GetAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        return Ok(_mapper.Map<TranslationEngineDto>(engine));
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
        var newEngine = new TranslationEngine
        {
            Name = engineConfig.Name,
            SourceLanguage = engineConfig.SourceLanguage,
            TargetLanguage = engineConfig.TargetLanguage,
            Type = engineConfig.Type,
            Owner = Owner
        };

        await _translationEngineService.CreateAsync(newEngine, cancellationToken);
        TranslationEngineDto dto = _mapper.Map<TranslationEngineDto>(newEngine);
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
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        if (!await _translationEngineService.DeleteAsync(id, cancellationToken))
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
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        TranslationResult? result = await _translationEngineService.TranslateAsync(
            engine.Id,
            segment,
            cancellationToken
        );
        if (result == null)
            return NotFound();
        return Ok(_mapper.Map<TranslationResultDto>(result));
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
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        IEnumerable<TranslationResult>? results = await _translationEngineService.TranslateAsync(
            engine.Id,
            n,
            segment,
            cancellationToken
        );
        if (results == null)
            return NotFound();
        return Ok(results.Select(_mapper.Map<TranslationResultDto>));
    }

    /// <summary>
    /// Gets the word graph that represents all possible translations of a segment of text.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="segment">The source segment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The word graph.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="405">The method is not supported.</response>
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
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        WordGraph? result = await _translationEngineService.GetWordGraphAsync(engine.Id, segment, cancellationToken);
        if (result == null)
            return NotFound();
        return Ok(_mapper.Map<WordGraphDto>(result));
    }

    /// <summary>
    /// Incrementally trains a translation engine with a segment pair.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="segmentPair">The segment pair.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The engine was trained successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="405">The method is not supported.</response>
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
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        if (
            !await _translationEngineService.TrainSegmentPairAsync(
                engine.Id,
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
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The corpus was added successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/corpora")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CorpusDto>> AddCorporaAsync(
        [NotNull] string id,
        [FromBody] CorpusConfigDto corpusConfig,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        var corpus = new Corpus
        {
            Name = corpusConfig.Name,
            SourceLanguage = corpusConfig.SourceLanguage,
            TargetLanguage = corpusConfig.TargetLanguage,
            Pretranslate = corpusConfig.Pretranslate ?? false
        };
        foreach (CorpusFileConfigDto fileConfig in corpusConfig.SourceFiles)
        {
            DataFileResult? dataFileResult = await _dataFileRetriever.GetDataFileAsync(
                fileConfig.FileId,
                Owner,
                cancellationToken
            );
            if (dataFileResult is null)
                return UnprocessableEntity();
            corpus.SourceFiles.Add(
                new CorpusFile
                {
                    Id = fileConfig.FileId,
                    Filename = dataFileResult.Filename,
                    TextId = fileConfig.TextId
                }
            );
        }
        foreach (CorpusFileConfigDto fileConfig in corpusConfig.TargetFiles)
        {
            DataFileResult? dataFileResult = await _dataFileRetriever.GetDataFileAsync(
                fileConfig.FileId,
                Owner,
                cancellationToken
            );
            if (dataFileResult is null)
                return UnprocessableEntity();
            corpus.TargetFiles.Add(
                new CorpusFile
                {
                    Id = fileConfig.FileId,
                    Filename = dataFileResult.Filename,
                    TextId = fileConfig.TextId
                }
            );
        }
        await _translationEngineService.AddCorpusAsync(id, corpus, cancellationToken);
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
    public async Task<ActionResult<IEnumerable<CorpusDto>>> GetAllCorporaAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
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
    [HttpGet("{id}/corpora/{corpusId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CorpusDto>> GetCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
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
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        if (!await _translationEngineService.DeleteCorpusAsync(id, corpusId, cancellationToken))
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
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        return Ok(
            (await _pretranslationService.GetAllAsync(id, corpusId, cancellationToken)).Select(
                _mapper.Map<PretranslationDto>
            )
        );
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
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        return Ok(
            (await _pretranslationService.GetAllAsync(id, corpusId, textId, cancellationToken)).Select(
                _mapper.Map<PretranslationDto>
            )
        );
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
    public async Task<ActionResult<IEnumerable<BuildDto>>> GetAllBuildsAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        return Ok((await _buildService.GetAllAsync(id, cancellationToken)).Select(_mapper.Map<BuildDto>));
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
    [HttpGet("{id}/builds/{buildId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    public async Task<ActionResult<BuildDto>> GetBuildAsync(
        [NotNull] string id,
        [NotNull] string buildId,
        [FromQuery] long? minRevision,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

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
                _ => Ok(_mapper.Map<BuildDto>(change.Entity!)),
            };
        }
        else
        {
            Build? build = await _buildService.GetAsync(buildId, cancellationToken);
            if (build == null)
                return NotFound();

            return Ok(_mapper.Map<BuildDto>(build));
        }
    }

    /// <summary>
    /// Starts a build job for a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="201">The build job was started successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BuildDto>> CreateBuildAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        Build? build = await _translationEngineService.StartBuildAsync(id, cancellationToken);
        if (build == null)
            return NotFound();
        var dto = _mapper.Map<BuildDto>(build);
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
    public async Task<ActionResult<BuildDto>> GetCurrentBuildAsync(
        [NotNull] string id,
        [FromQuery] long? minRevision,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

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
                _ => Ok(_mapper.Map<BuildDto>(change.Entity!)),
            };
        }
        else
        {
            Build? build = await _buildService.GetActiveAsync(id, cancellationToken);
            if (build == null)
                return NoContent();

            return Ok(_mapper.Map<BuildDto>(build));
        }
    }

    /// <summary>
    /// Cancels the current build job for a translation engine.
    /// </summary>
    /// <param name="id">The translation engine id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">The build job was cancelled successfully.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/current-build/cancel")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CancelBuildAsync([NotNull] string id, CancellationToken cancellationToken)
    {
        TranslationEngine? engine = await _translationEngineService.GetAsync(id, cancellationToken);
        if (engine == null)
            return NotFound();
        if (!await AuthorizeIsOwnerAsync(engine))
            return Forbid();

        await _translationEngineService.CancelBuildAsync(id, cancellationToken);
        return Ok();
    }

    private CorpusDto Map(string engineId, Corpus corpus)
    {
        return _mapper.Map<CorpusDto>(corpus, opts => opts.Items["EngineId"] = engineId);
    }
}
