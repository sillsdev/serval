﻿namespace Serval.Assessment.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/assessment/engines")]
[OpenApiTag("Assessment")]
[FeatureGate("Assessment")]
public class AssessmentEnginesController(
    IAuthorizationService authService,
    IEngineService engineService,
    IJobService jobService,
    IResultService resultService,
    IOptionsMonitor<ApiOptions> apiOptions,
    IUrlService urlService
) : ServalControllerBase(authService)
{
    private static readonly JsonSerializerOptions ObjectJsonSerializerOptions =
        new() { Converters = { new ObjectToInferredTypesConverter() } };

    private readonly IEngineService _engineService = engineService;
    private readonly IJobService _jobService = jobService;
    private readonly IResultService _resultService = resultService;
    private readonly IOptionsMonitor<ApiOptions> _apiOptions = apiOptions;
    private readonly IUrlService _urlService = urlService;

    /// <summary>
    /// Get all assessment engines.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engines</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadAssessmentEngines)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IEnumerable<AssessmentEngineDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return (await _engineService.GetAllAsync(Owner, cancellationToken)).Select(Map);
    }

    /// <summary>
    /// Get an assessment engine.
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engine</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>

    [Authorize(Scopes.ReadAssessmentEngines)]
    [HttpGet("{id}", Name = "GetAssessmentEngine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssessmentEngineDto>> GetAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        return Ok(Map(engine));
    }

    /// <summary>
    /// Create a new assessment engine.
    /// </summary>
    /// <param name="engineConfig">The engine configuration (see above)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new engine</response>
    /// <response code="400">Bad request. Is the engine type correct?</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.CreateAssessmentEngines)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssessmentEngineDto>> CreateAsync(
        [FromBody] AssessmentEngineConfigDto engineConfig,
        CancellationToken cancellationToken
    )
    {
        Engine engine = Map(engineConfig);
        Engine updatedEngine = await _engineService.CreateAsync(engine, cancellationToken);
        AssessmentEngineDto dto = Map(updatedEngine);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Delete an assessment engine.
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engine was successfully deleted.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist and therefore cannot be deleted.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.DeleteAssessmentEngines)]
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
    /// Get all assessment jobs.
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The jobs</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadAssessmentEngines)]
    [HttpGet("{id}/jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<Contracts.AssessmentJobDto>>> GetAllJobsAsync(
        [NotNull] string id,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        return Ok((await _jobService.GetAllAsync(id, cancellationToken)).Select(Map));
    }

    /// <summary>
    /// Get an assessment job.
    /// </summary>
    /// <remarks>
    /// If the `minRevision` is not defined, the current job, at whatever state it is,
    /// will be immediately returned.  If `minRevision` is defined, Serval will wait for
    /// up to 40 seconds for the engine to job to the `minRevision` specified, else
    /// will timeout.
    /// A use case is to actively query the state of the current job, where the subsequent
    /// request sets the `minRevision` to the returned `revision` + 1 and timeouts are handled gracefully.
    /// This method should use request throttling.
    /// Note: Within the returned job, percentCompleted is a value between 0 and 1.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="jobId">The job id</param>
    /// <param name="minRevision">The minimum revision</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The job</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the engine.</response>
    /// <response code="404">The engine or job does not exist.</response>
    /// <response code="408">The long polling request timed out. This is expected behavior if you're using long-polling with the minRevision strategy specified in the docs.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadAssessmentEngines)]
    [HttpGet("{id}/jobs/{jobId}", Name = "GetAssessmentJob")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Contracts.AssessmentJobDto>> GetJobAsync(
        [NotNull] string id,
        [NotNull] string jobId,
        [FromQuery] long? minRevision,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        if (minRevision != null)
        {
            EntityChange<Job> change = await TaskEx.Timeout(
                ct => _jobService.GetNewerRevisionAsync(jobId, minRevision.Value, ct),
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
            Job job = await _jobService.GetAsync(jobId, cancellationToken);
            return Ok(Map(job));
        }
    }

    /// <summary>
    /// Start an assessment job.
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="jobConfig">The job config (see remarks)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new job</response>
    /// <response code="400">The job configuration was invalid.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateAssessmentEngines)]
    [HttpPost("{id}/jobs")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Contracts.AssessmentJobDto>> StartJobAsync(
        [NotNull] string id,
        [FromBody] AssessmentJobConfigDto jobConfig,
        CancellationToken cancellationToken
    )
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
        Job job = Map(engine, jobConfig);
        await _engineService.StartJobAsync(job, cancellationToken);

        Contracts.AssessmentJobDto dto = Map(job);
        return Created(dto.Url, dto);
    }

    /// <summary>
    /// Delete an assessment job.
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The job was successfully deleted.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine does not exist and therefore cannot be deleted.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.DeleteAssessmentEngines)]
    [HttpDelete("{id}/jobs/{jobId}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteJobAsync(
        [NotNull] string id,
        [NotNull] string jobId,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        await _jobService.DeleteAsync(jobId, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Cancel an assessment job.
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="jobId">The job id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The job was cancelled successfully.</response>
    /// <response code="204">The job is not active.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="405">The engine does not support canceling jobs.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateAssessmentEngines)]
    [HttpPost("{id}/jobs/{jobId}/cancel")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> CancelJobAsync(
        [NotNull] string id,
        [NotNull] string jobId,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);
        if (!await _engineService.CancelJobAsync(id, jobId, cancellationToken))
            return NoContent();
        return Ok();
    }

    /// <summary>
    /// Get all results of an assessment job.
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="jobId">The job id</param>
    /// <param name="textId">The text id (optional)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The results</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadAssessmentEngines)]
    [HttpGet("{id}/jobs/{jobId}/results")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<AssessmentResultDto>>> GetAllResultsAsync(
        [NotNull] string id,
        [NotNull] string jobId,
        [FromQuery] string? textId,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);

        IEnumerable<Result> results = await _resultService.GetAllAsync(id, jobId, textId, cancellationToken);
        return Ok(results.Select(Map));
    }

    /// <summary>
    /// Get all results for the specified text of an assessment job.
    /// </summary>
    /// <param name="id">The engine id</param>
    /// <param name="jobId">The job id</param>
    /// <param name="textId">The text id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The results</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="409">The engine needs to be built first.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadAssessmentEngines)]
    [HttpGet("{id}/jobs/{jobId}/results/{textId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<AssessmentResultDto>>> GetResultsByTextIdAsync(
        [NotNull] string id,
        [NotNull] string jobId,
        [NotNull] string textId,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeAsync(id, cancellationToken);

        IEnumerable<Result> results = await _resultService.GetAllAsync(id, jobId, textId, cancellationToken);
        return Ok(results.Select(Map));
    }

    private async Task AuthorizeAsync(string id, CancellationToken cancellationToken)
    {
        Engine engine = await _engineService.GetAsync(id, cancellationToken);
        await AuthorizeAsync(engine);
    }

    private AssessmentEngineDto Map(Engine source)
    {
        return new AssessmentEngineDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl("GetAssessmentEngine", new { id = source.Id }),
            Name = source.Name,
            Type = source.Type.ToKebabCase(),
            Corpus = new ResourceLinkDto
            {
                Id = source.CorpusRef,
                Url = _urlService.GetUrl("GetAssessmentCorpus", new { id = source.CorpusRef })
            },
            ReferenceCorpus = source.ReferenceCorpusRef is null
                ? null
                : new ResourceLinkDto
                {
                    Id = source.ReferenceCorpusRef,
                    Url = _urlService.GetUrl("GetAssessmentCorpus", new { id = source.ReferenceCorpusRef })
                }
        };
    }

    private Engine Map(AssessmentEngineConfigDto source)
    {
        return new Engine
        {
            Name = source.Name,
            Type = source.Type.ToPascalCase(),
            Owner = Owner,
            CorpusRef = source.CorpusId,
            ReferenceCorpusRef = source.ReferenceCorpusId
        };
    }

    private static AssessmentResultDto Map(Result source)
    {
        return new AssessmentResultDto
        {
            TextId = source.TextId,
            Ref = source.Ref,
            Score = source.Score,
            Description = source.Description
        };
    }

    private Contracts.AssessmentJobDto Map(Job source)
    {
        return new Contracts.AssessmentJobDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl("GetAssessmentJob", new { id = source.EngineRef, jobId = source.Id }),
            Revision = source.Revision,
            Name = source.Name,
            Engine = new ResourceLinkDto
            {
                Id = source.EngineRef,
                Url = _urlService.GetUrl("GetAssessmentEngine", new { id = source.EngineRef })
            },
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange,
            PercentCompleted = source.PercentCompleted,
            Message = source.Message,
            State = source.State,
            DateFinished = source.DateFinished,
            Options = source.Options
        };
    }

    private static Job Map(Engine engine, AssessmentJobConfigDto source)
    {
        if (source.TextIds is not null && source.ScriptureRange is not null)
            throw new InvalidOperationException("Set at most one of TextIds and ScriptureRange.");

        return new Job
        {
            EngineRef = engine.Id,
            Name = source.Name,
            TextIds = source.TextIds?.ToList(),
            ScriptureRange = source.ScriptureRange,
            Options = Map(source.Options)
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
}