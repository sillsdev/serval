namespace Serval.Assessment.Consumers;

public class DataFileDeletedConsumer(IEngineService engineService) : IConsumer<DataFileDeleted>
{
    private readonly IEngineService _engineService = engineService;

    public Task Consume(ConsumeContext<DataFileDeleted> context)
    {
        return _engineService.DeleteAllCorpusFilesAsync(context.Message.DataFileId, context.CancellationToken);
    }
}
