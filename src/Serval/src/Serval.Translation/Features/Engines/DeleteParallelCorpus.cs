namespace Serval.Translation.Features.Engines;

public record DeleteParallelCorpus(string Owner, string EngineId, string ParallelCorpusId) : IRequest;

public class DeleteParallelCorpusHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations
) : IRequestHandler<DeleteParallelCorpus>
{
    public async Task HandleAsync(DeleteParallelCorpus request, CancellationToken cancellationToken = default)
    {
        await dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await engines.CheckOwnerAsync(request.EngineId, request.Owner, ct);

                Engine? originalEngine = await engines.UpdateAsync(
                    e => e.Id == request.EngineId,
                    u => u.RemoveAll(e => e.ParallelCorpora, c => c.Id == request.ParallelCorpusId),
                    returnOriginal: true,
                    cancellationToken: ct
                );
                if (
                    originalEngine is null
                    || !originalEngine.ParallelCorpora.Any(c => c.Id == request.ParallelCorpusId)
                )
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{request.ParallelCorpusId}' in Engine '{request.EngineId}'."
                    );
                }
                await pretranslations.DeleteAllAsync(pt => pt.CorpusRef == request.ParallelCorpusId, ct);
            },
            cancellationToken
        );
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Remove a parallel corpus from a translation engine
    /// </summary>
    /// <remarks>
    /// Removing a parallel corpus will remove all pretranslations associated with that corpus.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="parallelCorpusId">The parallel corpus id</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The parallel corpus was deleted successfully.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or parallel corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpDelete("{id}/parallel-corpora/{parallelCorpusId}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteParallelCorpusAsync(
        [NotNull] string id,
        [NotNull] string parallelCorpusId,
        [FromServices] IRequestHandler<DeleteParallelCorpus> handler,
        CancellationToken cancellationToken
    )
    {
        await handler.HandleAsync(new(Owner, id, parallelCorpusId), cancellationToken);
        return Ok();
    }
}
