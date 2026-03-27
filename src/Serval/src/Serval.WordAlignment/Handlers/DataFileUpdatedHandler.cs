namespace Serval.WordAlignment.Handlers;

public class DataFileUpdatedHandler(IEngineService engineService) : IEventHandler<DataFileUpdated>
{
    public Task HandleAsync(DataFileUpdated evt, CancellationToken cancellationToken)
    {
        return engineService.UpdateDataFileFilenameFilesAsync(evt.DataFileId, evt.Filename, cancellationToken);
    }
}
