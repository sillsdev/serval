namespace Serval.Translation.Consumers;

public class DataFileDeletedConsumer : IConsumer<DataFileDeleted>
{
    private readonly IEngineService _engineService;

    public DataFileDeletedConsumer(IEngineService engineService)
    {
        _engineService = engineService;
    }

    public async Task Consume(ConsumeContext<DataFileDeleted> context)
    {
        await _engineService.DeleteAllCorpusFilesAsync(context.Message.DataFileId, context.CancellationToken);
    }
}
