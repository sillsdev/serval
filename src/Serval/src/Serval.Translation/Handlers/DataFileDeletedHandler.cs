namespace Serval.Translation.Handlers;

public class DataFileDeletedHandler(IEngineService engineService) : IEventHandler<DataFileDeleted>
{
    public async Task HandleAsync(DataFileDeleted evt, CancellationToken cancellationToken)
    {
        await engineService.DeleteAllCorpusFilesAsync(evt.DataFileId, cancellationToken);
    }
}
