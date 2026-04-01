namespace Serval.Translation.Features.Engines;

public record DeleteEngine(string Owner, string EngineId) : IRequest;

public class DeleteEngineHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Build> builds,
    IRepository<Pretranslation> pretranslations,
    IEngineServiceFactory engineServiceFactory
) : IRequestHandler<DeleteEngine>
{
    public async Task HandleAsync(DeleteEngine request, CancellationToken cancellationToken = default)
    {
        await dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine? engine = await engines.GetAsync(request.EngineId, ct);
                if (engine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");
                if (engine.Owner != request.Owner)
                    throw new ForbiddenException();

                engine = await engines.DeleteAsync(request.EngineId, ct);
                if (engine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");

                await builds.DeleteAllAsync(b => b.EngineRef == request.EngineId, ct);
                await pretranslations.DeleteAllAsync(pt => pt.EngineRef == request.EngineId, ct);

                await engineServiceFactory.GetEngineService(engine.Type).DeleteAsync(request.EngineId, ct);
            },
            cancellationToken
        );
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Delete a translation engine
    /// </summary>
    /// <param name="id">The translation engine id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The engine was successfully deleted.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine does not exist and therefore cannot be deleted.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.DeleteTranslationEngines)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<DeleteEngine> handler,
        CancellationToken cancellationToken
    )
    {
        await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok();
    }
}
