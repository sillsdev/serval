namespace Serval.WordAlignment.Consumers;

public class DataFileUpdatedConsumer(IEngineService engineService) : IConsumer<DataFileUpdated>
{
    private readonly IEngineService _engineService = engineService;

    public async Task Consume(ConsumeContext<DataFileUpdated> context)
    {
        await _engineService.UpdateDataFileFilenameFilesAsync(
            context.Message.DataFileId,
            context.Message.Filename,
            context.CancellationToken
        );
    }
}
