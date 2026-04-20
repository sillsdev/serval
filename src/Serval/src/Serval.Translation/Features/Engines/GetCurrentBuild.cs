namespace Serval.Translation.Features.Engines;

public record GetCurrentBuild(string Owner, string EngineId, long? MinRevision) : IRequest<GetCurrentBuildResponse>;

public record GetCurrentBuildResponse(GetCurrentBuildStatus Status, TranslationBuildDto? Build = null);

public enum GetCurrentBuildStatus
{
    Found,
    NotActive,
    Timeout,
}

public class GetCurrentBuildHandler(
    IRepository<Engine> engines,
    IRepository<Build> builds,
    DtoMapper mapper,
    IOptionsMonitor<ApiOptions> apiOptions
) : IRequestHandler<GetCurrentBuild, GetCurrentBuildResponse>
{
    public async Task<GetCurrentBuildResponse> HandleAsync(
        GetCurrentBuild request,
        CancellationToken cancellationToken = default
    )
    {
        await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);

        if (request.MinRevision is not null)
        {
            (_, EntityChange<Build> change) = await TaskEx.Timeout(
                ct =>
                    builds.GetNewerRevisionAsync(
                        b =>
                            b.EngineRef == request.EngineId
                            && (b.State == JobState.Active || b.State == JobState.Pending),
                        request.MinRevision.Value,
                        ct
                    ),
                apiOptions.CurrentValue.LongPollTimeout,
                cancellationToken: cancellationToken
            );
            return change.Type switch
            {
                EntityChangeType.None => new(GetCurrentBuildStatus.Timeout),
                EntityChangeType.Delete => new(GetCurrentBuildStatus.NotActive),
                _ => new(GetCurrentBuildStatus.Found, mapper.Map(change.Entity!)),
            };
        }
        else
        {
            Build? build = await builds.GetAsync(
                b => b.EngineRef == request.EngineId && (b.State == JobState.Active || b.State == JobState.Pending),
                cancellationToken
            );
            if (build is null)
                return new(GetCurrentBuildStatus.NotActive);
            return new(GetCurrentBuildStatus.Found, mapper.Map(build));
        }
    }
}

public partial class TranslationEnginesController
{
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
        [FromServices] IRequestHandler<GetCurrentBuild, GetCurrentBuildResponse> handler,
        CancellationToken cancellationToken
    )
    {
        minRevision ??= minRevisionCamelCase;
        GetCurrentBuildResponse response = await handler.HandleAsync(new(Owner, id, minRevision), cancellationToken);
        return response.Status switch
        {
            GetCurrentBuildStatus.Timeout => StatusCode(StatusCodes.Status408RequestTimeout),
            GetCurrentBuildStatus.NotActive => NoContent(),
            _ => Ok(response.Build),
        };
    }
}
