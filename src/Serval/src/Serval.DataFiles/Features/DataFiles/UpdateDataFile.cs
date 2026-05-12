namespace Serval.DataFiles.Features.DataFiles;

public record UpdateDataFile(string Owner, string FileId, Stream FileStream) : IRequest<UpdateDataFileResponse>;

public record UpdateDataFileResponse(DataFileDto DataFile);

public class UpdateDataFileHandler(
    IRepository<DataFile> dataFiles,
    IRepository<DeletedFile> deletedFiles,
    IDataAccessContext dataAccessContext,
    IOptionsMonitor<DataFileOptions> options,
    IEventRouter eventRouter,
    IFileSystem fileSystem,
    DtoMapper mapper
) : IRequestHandler<UpdateDataFile, UpdateDataFileResponse>
{
    public async Task<UpdateDataFileResponse> HandleAsync(UpdateDataFile request, CancellationToken cancellationToken)
    {
        await dataFiles.CheckOwnerAsync(request.FileId, request.Owner, cancellationToken);

        string filename = Path.GetRandomFileName();
        string path = Path.Combine(options.CurrentValue.FilesDirectory, filename);
        bool deleteFile = false;
        try
        {
            await using (Stream fileStream = fileSystem.OpenWrite(path))
                await request.FileStream.CopyToAsync(fileStream, cancellationToken);
            await dataAccessContext.WithTransactionAsync(
                async ct =>
                {
                    DataFile? originalDataFile = await dataFiles.UpdateAsync(
                        request.FileId,
                        u => u.Set(f => f.Filename, filename),
                        returnOriginal: true,
                        cancellationToken: ct
                    );
                    if (originalDataFile is null)
                        throw new EntityNotFoundException($"Could not find the DataFile '{request.FileId}'.");
                    await deletedFiles.InsertAsync(
                        new DeletedFile { Filename = originalDataFile.Filename, DeletedAt = DateTime.UtcNow },
                        ct
                    );
                    await eventRouter.PublishAsync(new DataFileUpdated(request.FileId, filename), ct);
                },
                cancellationToken: cancellationToken
            );
        }
        catch
        {
            deleteFile = true;
            throw;
        }
        finally
        {
            if (deleteFile)
                fileSystem.DeleteFile(path);
        }

        DataFile? dataFile = await dataFiles.GetAsync(request.FileId, cancellationToken);
        if (dataFile is null)
            throw new EntityNotFoundException($"Could not find the DataFile '{request.FileId}'.");
        return new(mapper.Map(dataFile));
    }
}

public partial class DataFilesController
{
    /// <summary>
    /// Update an existing file
    /// </summary>
    /// <param name="id">The existing file's unique id</param>
    /// <param name="file">The updated file</param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The file was updated successfully</response>
    /// <response code="400">Bad request. Is the file over 100 MB?</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation or does not own the file</response>
    /// <response code="404">The file does not exist and therefore cannot be updated</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.UpdateFiles)]
    [HttpPatch("{id}")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DataFileDto>> UpdateAsync(
        [NotNull] string id,
        [BindRequired] IFormFile file,
        [FromServices] IRequestHandler<UpdateDataFile, UpdateDataFileResponse> handler,
        CancellationToken cancellationToken
    )
    {
        using Stream stream = file.OpenReadStream();
        UpdateDataFileResponse response = await handler.HandleAsync(new(Owner, id, stream), cancellationToken);
        return Ok(response.DataFile);
    }
}
