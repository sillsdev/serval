namespace Serval.DataFiles.Features.Corpora;

public record DeleteCorpus(string Owner, string CorpusId) : IRequest;

public class DeleteCorpusHandler(IRepository<Corpus> corpora) : IRequestHandler<DeleteCorpus>
{
    public async Task HandleAsync(DeleteCorpus request, CancellationToken cancellationToken)
    {
        await corpora.CheckOwnerAsync(request.CorpusId, request.Owner, cancellationToken);
        Corpus? corpus = await corpora.DeleteAsync(request.CorpusId, cancellationToken);
        if (corpus is null)
            throw new EntityNotFoundException($"Could not find the Corpus '{request.CorpusId}'.");
    }
}

public partial class CorporaController
{
    /// <summary>
    /// Delete an existing corpus
    /// </summary>
    /// <param name="id">The unique identifier for the corpus</param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The corpus was deleted successfully</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the corpus</response>
    /// <response code="404">The corpus does not exist and therefore cannot be deleted</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.DeleteFiles)]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DeleteAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<DeleteCorpus> handler,
        CancellationToken cancellationToken
    )
    {
        await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok();
    }
}
