namespace Serval.Translation.Features.Engines;

public record GetBuild(string Owner, string EngineId, string BuildId, long? MinRevision) : IRequest<GetBuildResponse>;

public record GetBuildResponse(GetBuildStatus Status, TranslationBuildDto? Build = null);

public enum GetBuildStatus
{
    Found,
    Deleted,
    Timeout,
}

public class GetBuildHandler(
    IRepository<Engine> engines,
    IRepository<Build> builds,
    DtoMapper mapper,
    IOptionsMonitor<ApiOptions> apiOptions
) : IRequestHandler<GetBuild, GetBuildResponse>
{
    public async Task<GetBuildResponse> HandleAsync(GetBuild request, CancellationToken cancellationToken = default)
    {
        await engines.CheckOwnerAsync(request.EngineId, request.Owner, cancellationToken);

        if (request.MinRevision is not null)
        {
            (_, EntityChange<Build> change) = await TaskEx.Timeout(
                ct => builds.GetNewerRevisionAsync(e => e.Id == request.BuildId, request.MinRevision.Value, ct),
                apiOptions.CurrentValue.LongPollTimeout,
                cancellationToken: cancellationToken
            );
            return change.Type switch
            {
                EntityChangeType.None => new(GetBuildStatus.Timeout),
                EntityChangeType.Delete => new(GetBuildStatus.Deleted),
                _ => new(GetBuildStatus.Found, mapper.Map(change.Entity!)),
            };
        }
        else
        {
            Build? build = await builds.GetAsync(e => e.Id == request.BuildId, cancellationToken);
            if (build is null)
                throw new EntityNotFoundException($"Could not find the Build '{request.BuildId}'.");
            return new(GetBuildStatus.Found, mapper.Map(build));
        }
    }
}

public partial class TranslationEnginesController
{
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
        [FromServices] IRequestHandler<GetBuild, GetBuildResponse> handler,
        CancellationToken cancellationToken
    )
    {
        minRevision ??= minRevisionCamelCase;
        GetBuildResponse response = await handler.HandleAsync(new(Owner, id, buildId, minRevision), cancellationToken);
        return response.Status switch
        {
            GetBuildStatus.Timeout => StatusCode(StatusCodes.Status408RequestTimeout),
            GetBuildStatus.Deleted => NotFound(),
            _ => Ok(response.Build),
        };
    }
}
