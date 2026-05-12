namespace Serval.DataFiles.Features.DataFiles;

public record DeleteDataFile(string Owner, string FileId) : IRequest;

public class DeleteDataFileHandler(IRepository<DataFile> dataFiles, DataFileDeleter deleter)
    : IRequestHandler<DeleteDataFile>
{
    public async Task HandleAsync(DeleteDataFile request, CancellationToken cancellationToken)
    {
        await dataFiles.CheckOwnerAsync(request.FileId, request.Owner, cancellationToken);
        await deleter.DeleteAsync(request.FileId, cancellationToken);
    }
}

public partial class DataFilesController
{
    /// <summary>
    /// Delete an existing file
    /// </summary>
    /// <remarks>
    /// If a file is in a corpus and the file is deleted, it will be automatically removed from that corpus.
    /// If a build job has started before the file was deleted, the file will be used for the build job, even
    /// though it will no longer be accessible through the API.
    /// </remarks>
    /// <param name="id">The existing file's unique id</param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The file was deleted successfully</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the file</response>
    /// <response code="404">The file does not exist and therefore cannot be deleted</response>
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
        [FromServices] IRequestHandler<DeleteDataFile> handler,
        CancellationToken cancellationToken
    )
    {
        await handler.HandleAsync(new(Owner, id), cancellationToken);
        return Ok();
    }
}
