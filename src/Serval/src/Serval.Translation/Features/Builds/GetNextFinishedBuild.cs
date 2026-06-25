namespace Serval.Translation.Features.Builds;

public record GetNextFinishedBuild(string Owner, string? Id = null) : IRequest<GetNextFinishedBuildResponse>;

public record GetNextFinishedBuildResponse(
    [property: MemberNotNullWhen(false, nameof(Build))] bool TimedOut,
    TranslationBuildDto? Build = null
);

public class GetNextFinishedBuildHandler(
    IRepository<Build> builds,
    DtoMapper mapper,
    IOptionsMonitor<ApiOptions> apiOptions
) : IRequestHandler<GetNextFinishedBuild, GetNextFinishedBuildResponse>
{
    public async Task<GetNextFinishedBuildResponse> HandleAsync(
        GetNextFinishedBuild request,
        CancellationToken cancellationToken = default
    )
    {
        DateTime dateFinished = DateTime.UtcNow;
        string? id = request.Id;
        if (id is not null)
        {
            Build? build = await builds.GetAsync(e => e.Id == id && e.Owner == request.Owner, cancellationToken);
            if (build is not null)
                dateFinished = build.DateFinished ?? DateTime.UtcNow;
        }

        (_, EntityChange<Build> change) = await TaskEx.Timeout(
            async ct =>
            {
                using ISubscription<Build> subscription = await builds.SubscribeAsync(
                    b =>
                        b.Owner == request.Owner
                        && (
                            b.State == JobState.Completed || b.State == JobState.Canceled || b.State == JobState.Faulted
                        )
                        && (
                            b.DateFinished > dateFinished || (b.DateFinished == dateFinished && b.Id.CompareTo(id) > 0)
                        ),
                    [(b => b.DateFinished, SortOrder.Ascending), (b => b.Id, SortOrder.Ascending)],
                    SubscriptionMode.Repository,
                    ct
                );
                EntityChange<Build> curChange = subscription.Change;
                while (true)
                {
                    if (curChange.Type is not EntityChangeType.None and not EntityChangeType.Delete)
                        return curChange;
                    await subscription.WaitForChangeAsync(cancellationToken: ct);
                    curChange = subscription.Change;
                }
            },
            apiOptions.CurrentValue.LongPollTimeout,
            cancellationToken: cancellationToken
        );
        return change.Type switch
        {
            EntityChangeType.None => new(TimedOut: true),
            _ => change.Entity is null ? new(TimedOut: true) : new(TimedOut: false, mapper.Map(change.Entity)),
        };
    }
}

public partial class TranslationBuildsController
{
    /// <summary>
    /// Get the next build that finishes after the specified build id.
    /// If no build has yet completed after that id, or you do not specify the id,
    /// Serval will wait until the next build is finished.
    /// </summary>
    /// <param name="finishedAfter">The id of the build that the next build must finish after (optional)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The build</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation.</response>
    /// <response code="408">The long polling request timed out.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("next-finished")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationBuildDto>> GetNextFinishedBuildAsync(
        [FromQuery(Name = "finished-after")] string? finishedAfter,
        [FromServices] IRequestHandler<GetNextFinishedBuild, GetNextFinishedBuildResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetNextFinishedBuildResponse response = await handler.HandleAsync(new(Owner, finishedAfter), cancellationToken);
        return response.TimedOut ? StatusCode(StatusCodes.Status408RequestTimeout) : Ok(response.Build);
    }
}
