namespace Serval.DataFiles.Consumers;

public class DeleteDataFileConsumer(IDataFileService dataFileService) : IConsumer<DeleteDataFile>
{
    private readonly IDataFileService _dataFileService = dataFileService;

    public async Task Consume(ConsumeContext<DeleteDataFile> context)
    {
        await _dataFileService.DeleteAsync(context.Message.DataFileId, context.CancellationToken);
    }
}
