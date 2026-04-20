namespace Serval.Translation.Features.Engines;

#pragma warning disable CS0612 // Type or member is obsolete

public record DeleteCorpus(string Owner, string EngineId, string CorpusId, bool DeleteFiles) : IRequest;

public class DeleteCorpusHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    IRequestHandler<DeleteDataFile> deleteDataFileHandler
) : IRequestHandler<DeleteCorpus>
{
    public async Task HandleAsync(DeleteCorpus request, CancellationToken cancellationToken = default)
    {
        await dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await engines.CheckOwnerAsync(request.EngineId, request.Owner, ct);

                Engine? originalEngine = await engines.UpdateAsync(
                    e => e.Id == request.EngineId,
                    u => u.RemoveAll(e => e.Corpora, c => c.Id == request.CorpusId),
                    returnOriginal: true,
                    cancellationToken: ct
                );
                if (originalEngine is null || !originalEngine.Corpora.Any(c => c.Id == request.CorpusId))
                {
                    throw new EntityNotFoundException(
                        $"Could not find the Corpus '{request.CorpusId}' in Engine '{request.EngineId}'."
                    );
                }
                await pretranslations.DeleteAllAsync(pt => pt.CorpusRef == request.CorpusId, ct);

                if (request.DeleteFiles)
                {
                    foreach (
                        string id in originalEngine.Corpora.SelectMany(c =>
                            c.TargetFiles.Select(f => f.Id).Concat(c.SourceFiles.Select(f => f.Id)).Distinct()
                        )
                    )
                    {
                        await deleteDataFileHandler.HandleAsync(new DeleteDataFile(id), ct);
                    }
                }
            },
            cancellationToken
        );
    }
}

public partial class TranslationEnginesController
{
    /// <summary>
    /// Remove a corpus from a translation engine (obsolete - use parallel corpora instead)
    /// </summary>
    /// <remarks>
    /// Removing a corpus will remove all pretranslations associated with that corpus.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="corpusId">The corpus id</param>
    /// <param name="deleteFiles">If `true`, all files associated with the corpus will be deleted as well (even if they are associated with other corpora). If false, no files will be deleted.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was deleted successfully.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the translation engine.</response>
    /// <response code="404">The engine or corpus does not exist.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Obsolete("This endpoint is obsolete. Use parallel corpora instead.")]
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpDelete("{id}/corpora/{corpusId}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteCorpusAsync(
        [NotNull] string id,
        [NotNull] string corpusId,
        [FromQuery(Name = "delete-files")] bool? deleteFiles,
        [FromServices] IRequestHandler<DeleteCorpus> handler,
        CancellationToken cancellationToken
    )
    {
        await handler.HandleAsync(new(Owner, id, corpusId, deleteFiles ?? false), cancellationToken);
        return Ok();
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
