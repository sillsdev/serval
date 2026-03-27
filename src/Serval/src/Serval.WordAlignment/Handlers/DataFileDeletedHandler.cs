namespace Serval.WordAlignment.Handlers;

public class DataFileDeletedHandler(IEngineService engineService) : IEventHandler<DataFileDeleted>
{
    public Task HandleAsync(DataFileDeleted evt, CancellationToken cancellationToken)
    {
        return engineService.DeleteAllCorpusFilesAsync(evt.DataFileId, cancellationToken);
    }
}
