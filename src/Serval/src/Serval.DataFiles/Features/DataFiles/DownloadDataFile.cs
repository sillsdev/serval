namespace Serval.DataFiles.Features.DataFiles;

public record DownloadDataFile(string Owner, string FileId) : IRequest<DownloadDataFileResponse>;

public record DownloadDataFileResponse(Stream FileStream, string? FileName);

public class DownloadDataFileHandler(
    IRepository<DataFile> dataFiles,
    IOptionsMonitor<DataFileOptions> options,
    IFileSystem fileSystem
) : IRequestHandler<DownloadDataFile, DownloadDataFileResponse>
{
    public async Task<DownloadDataFileResponse> HandleAsync(
        DownloadDataFile request,
        CancellationToken cancellationToken
    )
    {
        DataFile dataFile = await dataFiles.CheckOwnerAsync(request.FileId, request.Owner, cancellationToken);
        string path = Path.Combine(options.CurrentValue.FilesDirectory, dataFile.Filename);
        Stream stream = fileSystem.OpenRead(path);
        return new(stream, dataFile.Name);
    }
}

public partial class DataFilesController
{
    /// <summary>
    /// Download a file
    /// </summary>
    /// <param name="id">The unique identifier for the file</param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The file exists</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the file</response>
    /// <response code="404">The file does not exist</response>
    /// <response code="500">The data file is corrupted. Please try reuploading or recreating the file.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.ReadFiles)]
    [HttpPost("{id}/contents")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> DownloadAsync(
        [NotNull] string id,
        [FromServices] IRequestHandler<DownloadDataFile, DownloadDataFileResponse> handler,
        CancellationToken cancellationToken
    )
    {
        DownloadDataFileResponse response = await handler.HandleAsync(new(Owner, id), cancellationToken);
        return File(response.FileStream, "application/octet-stream", response.FileName);
    }
}
