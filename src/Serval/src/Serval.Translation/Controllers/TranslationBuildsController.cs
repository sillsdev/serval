namespace Serval.Translation.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/builds")]
[OpenApiTag("Translation Engines")]
public class TranslationBuildsController(
    IOptionsMonitor<ApiOptions> apiOptions,
    IAuthorizationService authService,
    IBuildService buildService,
    IUrlService urlService
) : TranslationControllerBase(authService, urlService)
{
    /// <summary>
    /// Get all builds for your translation engines that are created after the specified date.
    /// </summary>
    /// <param name="createdAfter">The date and time in UTC that the builds were created after (optional).</param>
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
    public async Task<IEnumerable<TranslationBuildDto>> GetAllBuildsCreatedAfterAsync(
        [FromQuery(Name = "created-after")] DateTime? createdAfter,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<Build> builds;
        if (createdAfter is null)
        {
            builds = await buildService.GetAllAsync(Owner, cancellationToken);
        }
        else
        {
            builds = await buildService.GetAllCreatedAfterAsync(Owner, createdAfter, cancellationToken);
        }
        return builds.Select(Map);
    }

    /// <summary>
    /// Get the next build that finished after the specified date and time.
    /// If not build has yet completed after that timestamp,
    /// Serval will wait until a build is finished after that date and time.
    /// </summary>
    /// <param name="finishedAfter">
    /// The date and time in UTC that the next build should have finished after.
    /// You should use the <c>finished</c> timestamp of the build previously returned when calling this endpoint.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engines</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation.</response>
    /// <response code="408">The long polling request timed out.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("next")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationBuildDto>> GetNextFinishedBuildAsync(
        [FromQuery(Name = "finished-after")] DateTime finishedAfter,
        CancellationToken cancellationToken
    )
    {
        (_, EntityChange<Build> change) = await TaskEx.Timeout(
            ct => buildService.GetNextFinishedBuildAsync(Owner, finishedAfter, ct),
            apiOptions.CurrentValue.LongPollTimeout,
            cancellationToken: cancellationToken
        );
        return change.Type switch
        {
            EntityChangeType.None => StatusCode(StatusCodes.Status408RequestTimeout),
            _ => change.Entity is null ? StatusCode(StatusCodes.Status408RequestTimeout) : Map(change.Entity),
        };
    }
}
