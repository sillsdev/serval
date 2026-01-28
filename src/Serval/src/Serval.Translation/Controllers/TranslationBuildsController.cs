namespace Serval.Translation.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/translation/builds")]
[OpenApiTag("Translation Engines")]
public class TranslationBuildsController(
    IAuthorizationService authService,
    IBuildService buildService,
    IUrlService urlService
) : TranslationControllerBase(authService, urlService)
{
    private readonly IBuildService _buildService = buildService;

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
            builds = await _buildService.GetAllAsync(Owner, cancellationToken);
        }
        else
        {
            builds = await _buildService.GetAllCreatedAfterAsync(Owner, createdAfter, cancellationToken);
        }
        return builds.Select(Map);
    }
}
