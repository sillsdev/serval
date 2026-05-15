namespace Serval.DataFiles.Handlers;

public class DeleteDataFileHandler(DataFileDeleter deleter) : IRequestHandler<DeleteDataFile>
{
    public Task HandleAsync(DeleteDataFile request, CancellationToken cancellationToken)
    {
        return deleter.DeleteAsync(request.DataFileId, cancellationToken);
    }
}
