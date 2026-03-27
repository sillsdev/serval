namespace Serval.DataFiles.Handlers;

public class DeleteDataFileHandler(IDataFileService dataFileService) : IRequestHandler<DeleteDataFile>
{
    public Task HandleAsync(DeleteDataFile request, CancellationToken cancellationToken)
    {
        return dataFileService.DeleteAsync(request.DataFileId, cancellationToken);
    }
}
