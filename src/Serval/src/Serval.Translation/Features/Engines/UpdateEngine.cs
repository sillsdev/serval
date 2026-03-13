namespace Serval.Translation.Features.Engines;

public class TranslationEngineUpdateConfigDto
{
    public string? SourceLanguage { get; init; }

    public string? TargetLanguage { get; init; }
}

public record UpdateEngine(string Owner, string EngineId, TranslationEngineUpdateConfigDto UpdateConfig) : IRequest;

public class UpdateEngineHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    IEngineServiceFactory engineServiceFactory
) : IRequestHandler<UpdateEngine>
{
    public Task HandleAsync(UpdateEngine request, CancellationToken cancellationToken)
    {
        return dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? engine = await engines.GetAsync(request.EngineId, ct);
                if (engine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");
                if (engine.Owner != request.Owner)
                    throw new ForbiddenException();

                engine = await engines.UpdateAsync(
                    request.EngineId,
                    u =>
                    {
                        if (request.UpdateConfig.SourceLanguage is not null)
                            u.Set(e => e.SourceLanguage, request.UpdateConfig.SourceLanguage);
                        if (request.UpdateConfig.TargetLanguage is not null)
                            u.Set(e => e.TargetLanguage, request.UpdateConfig.TargetLanguage);
                    },
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");
                await pretranslations.DeleteAllAsync(pt => pt.EngineRef == request.EngineId, ct);

                await engineServiceFactory
                    .GetEngineService(engine.Type)
                    .UpdateAsync(
                        request.EngineId,
                        request.UpdateConfig.SourceLanguage,
                        request.UpdateConfig.TargetLanguage,
                        ct
                    );
            },
            cancellationToken
        );
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Update the source and/or target languages of a translation engine
    /// </summary>
    /// <remarks>
    /// ## Sample request:
    ///
    ///     {
    ///       "sourceLanguage": "en",
    ///       "targetLanguage": "en"
    ///     }
    ///
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engine language was successfully updated.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist and therefore cannot be updated.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> UpdateAsync(
        [FromRoute] string id,
        [FromBody] TranslationEngineUpdateConfigDto request,
        [FromServices] IRequestHandler<UpdateEngine> handler,
        CancellationToken cancellationToken = default
    )
    {
        if (
            request is null
            || (string.IsNullOrWhiteSpace(request.SourceLanguage) && string.IsNullOrWhiteSpace(request.TargetLanguage))
        )
        {
            return BadRequest("sourceLanguage or targetLanguage is required.");
        }

        await handler.HandleAsync(new UpdateEngine(Owner, id, request), cancellationToken);

        return Ok();
    }
}
