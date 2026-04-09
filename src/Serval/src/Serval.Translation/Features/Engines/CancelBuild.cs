namespace Serval.Translation.Features.Engines;

public record CancelBuild(string Owner, string EngineId) : IRequest<CancelBuildResponse>;

public record CancelBuildResponse(
    [property: MemberNotNullWhen(true, nameof(Build))] bool IsBuildRunning,
    TranslationBuildDto? Build = null
);

public class CancelBuildHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Build> builds,
    IEngineServiceFactory engineServiceFactory,
    DtoMapper mapper
) : IRequestHandler<CancelBuild, CancelBuildResponse>
{
    public async Task<CancelBuildResponse> HandleAsync(CancelBuild request, CancellationToken cancellationToken)
    {
        return await dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? engine = await engines.GetAsync(request.EngineId, ct);
                if (engine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");
                if (engine.Owner != request.Owner)
                    throw new ForbiddenException();

                string? buildId = await engineServiceFactory
                    .GetEngineService(engine.Type)
                    .CancelBuildAsync(request.EngineId, ct);
                if (buildId is null)
                    return new CancelBuildResponse(IsBuildRunning: false);

                Build? currentBuild = await builds.GetAsync(buildId, ct);
                if (currentBuild is null)
                    return new CancelBuildResponse(IsBuildRunning: false);

                return new CancelBuildResponse(IsBuildRunning: true, mapper.Map(currentBuild));
            },
            cancellationToken: cancellationToken
        );
    }
}

public partial class TranslationEnginesController
{
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
    public async Task<ActionResult<TranslationBuildDto>> CancelBuildAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<CancelBuild, CancelBuildResponse> handler,
        CancellationToken cancellationToken
    )
    {
        CancelBuildResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        if (response.IsBuildRunning)
            return Ok(response.Build);
        return NoContent();
    }
}
