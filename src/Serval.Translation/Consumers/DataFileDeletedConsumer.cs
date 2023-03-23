namespace Serval.Translation.Consumers;

public class DataFileDeletedConsumer : IConsumer<DataFileDeleted>
{
    private readonly ITranslationEngineService _engineService;

    public DataFileDeletedConsumer(ITranslationEngineService engineService)
    {
        _engineService = engineService;
    }

    public async Task Consume(ConsumeContext<DataFileDeleted> context)
    {
        await _engineService.DeleteAllCorpusFilesAsync(context.Message.DataFileId, context.CancellationToken);
    }
}
