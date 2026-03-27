namespace Serval.Translation.Features.Engines;

public class ModelDownloadUrlDto
{
    public string Url { get; set; } = default!;
    public int ModelRevision { get; set; } = default!;
    public DateTime ExpiresAt { get; set; } = default!;
}

public record GetModelDownloadUrl(string Owner, string EngineId) : IRequest<GetModelDownloadUrlResponse>;

public record GetModelDownloadUrlResponse(bool IsModelAvailable, ModelDownloadUrlDto? ModelDownloadUrl = null);

public class GetModelDownloadUrlHandler(IRepository<Engine> engines, IEngineServiceFactory engineServiceFactory)
    : IRequestHandler<GetModelDownloadUrl, GetModelDownloadUrlResponse>
{
    public async Task<GetModelDownloadUrlResponse> HandleAsync(
        GetModelDownloadUrl request,
        CancellationToken cancellationToken
    )
    {
        Engine? engine = await engines.GetAsync(request.EngineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");
        if (engine.Owner != request.Owner)
            throw new ForbiddenException();

        if (engine.ModelRevision == 0)
            return new(IsModelAvailable: false);

        ModelDownloadUrl url = await engineServiceFactory
            .GetEngineService(engine.Type)
            .GetModelDownloadUrlAsync(engine.Id, cancellationToken);
        return new(
            IsModelAvailable: true,
            new ModelDownloadUrlDto
            {
                Url = url.Url,
                ModelRevision = url.ModelRevision,
                ExpiresAt = url.ExpiresAt,
            }
        );
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Get a link to download the NMT translation model of the last build that was successfully saved.
    /// </summary>
    /// <remarks>
    /// If an nmt build was successful and `isModelPersisted` is `true` for the engine,
    /// then the model from the most recent successful build can be downloaded.
    ///
    /// The endpoint will return a URL that can be used to download the model for up to 1 hour
    /// after the request is made.  If the URL is not used within that time, a new request will need to be made.
    ///
    /// The download itself is created by g-zipping together the folder containing the fine tuned model
    /// with all necessary supporting files.  This zipped folder is then named by the pattern:
    ///  * &lt;engine_id&gt;_&lt;model_revision&gt;.tar.gz
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The url to download the model.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="404">The engine does not exist or there is no saved model.</response>
    /// <response code="405">The translation engine does not support downloading builds.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadTranslationEngines)]
    [HttpGet("{id}/model-download-url")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ModelDownloadUrlDto>> GetModelDownloadUrlAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<GetModelDownloadUrl, GetModelDownloadUrlResponse> handler,
        CancellationToken cancellationToken
    )
    {
        GetModelDownloadUrlResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        if (response.IsModelAvailable)
            return Ok(response.ModelDownloadUrl);
        return NotFound();
    }
}
