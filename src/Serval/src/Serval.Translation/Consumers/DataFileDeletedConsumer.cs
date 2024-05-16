namespace Serval.Translation.Consumers;

public class DataFileDeletedConsumer(IEngineService engineService) : IConsumer<DataFileDeleted>
{
    private readonly IEngineService _engineService = engineService;

    public async Task Consume(ConsumeContext<DataFileDeleted> context)
    {
        await _engineService.DeleteAllCorpusFilesAsync(context.Message.DataFileId, context.CancellationToken);
    }
}
