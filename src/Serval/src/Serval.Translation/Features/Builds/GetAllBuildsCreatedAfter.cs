namespace Serval.Translation.Features.Builds;

public record GetAllBuildsCreatedAfter(string Owner, DateTime? CreatedAfter)
    : IRequest<GetAllBuildsCreatedAfterResponse>;

public record GetAllBuildsCreatedAfterResponse(IEnumerable<TranslationBuildDto> Builds);

public class GetAllBuildsCreatedAfterHandler(IRepository<Build> builds, DtoMapper mapper)
    : IRequestHandler<GetAllBuildsCreatedAfter, GetAllBuildsCreatedAfterResponse>
{
    public async Task<GetAllBuildsCreatedAfterResponse> HandleAsync(
        GetAllBuildsCreatedAfter request,
        CancellationToken cancellationToken = default
    )
    {
        DateTime? createdAfter = request.CreatedAfter;
        if (createdAfter is not null)
        {
            createdAfter =
                createdAfter.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(createdAfter.Value, DateTimeKind.Utc)
                    : createdAfter.Value.ToUniversalTime();
        }

        IEnumerable<Build> result = createdAfter is null
            ? await builds.GetAllAsync(b => b.Owner == request.Owner, cancellationToken)
            : await builds.GetAllAsync(
                b => b.Owner == request.Owner && b.DateCreated > createdAfter,
                cancellationToken
            );

        return new(result.Select(mapper.Map));
    }
}

public partial class TranslationBuildsController
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
        [FromServices] IRequestHandler<GetAllBuildsCreatedAfter, GetAllBuildsCreatedAfterResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetAllBuildsCreatedAfterResponse response = await handler.HandleAsync(
            new(Owner, createdAfter),
            cancellationToken
        );
        return response.Builds;
    }
}
