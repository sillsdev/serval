namespace Serval.Translation.Features.Builds;

public record GetNextFinishedBuild(string Owner, DateTime FinishedAfter) : IRequest<GetNextFinishedBuildResponse>;

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
        DateTime finishedAfter =
            request.FinishedAfter.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.FinishedAfter, DateTimeKind.Utc)
                : request.FinishedAfter.ToUniversalTime();

        (_, EntityChange<Build> change) = await TaskEx.Timeout(
            async ct =>
            {
                using ISubscription<Build> subscription = await builds.SubscribeAsync(
                    b =>
                        b.Owner == request.Owner
                        && (
                            b.State == JobState.Completed || b.State == JobState.Canceled || b.State == JobState.Faulted
                        )
                        && b.DateFinished > finishedAfter,
                    ct
                );
                EntityChange<Build> curChange = subscription.Change;
                while (true)
                {
                    if (curChange.Type is not EntityChangeType.None and not EntityChangeType.Delete)
                        return curChange;
                    await subscription.WaitForChangeAsync(
                        changeTypes: new HashSet<EntityChangeType> { EntityChangeType.Insert, EntityChangeType.Update },
                        cancellationToken: ct
                    );
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
    [HttpGet("next-finished")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationBuildDto>> GetNextFinishedBuildAsync(
        [FromQuery(Name = "finished-after")] DateTime finishedAfter,
        [FromServices] IRequestHandler<GetNextFinishedBuild, GetNextFinishedBuildResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetNextFinishedBuildResponse response = await handler.HandleAsync(new(Owner, finishedAfter), cancellationToken);
        return response.TimedOut ? StatusCode(StatusCodes.Status408RequestTimeout) : Ok(response.Build);
    }
}
