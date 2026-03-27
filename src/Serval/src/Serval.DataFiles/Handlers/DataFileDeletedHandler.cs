namespace Serval.DataFiles.Handlers;

public class DataFileDeletedHandler(ICorpusService corpusService) : IEventHandler<DataFileDeleted>
{
    public Task HandleAsync(DataFileDeleted evt, CancellationToken cancellationToken)
    {
        return corpusService.DeleteAllCorpusFilesAsync(evt.DataFileId, cancellationToken);
    }
}
