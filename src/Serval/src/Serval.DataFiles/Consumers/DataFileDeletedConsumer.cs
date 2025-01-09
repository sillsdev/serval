namespace Serval.DataFiles.Consumers;

public class DataFileDeletedConsumer(ICorpusService corpusService) : IConsumer<DataFileDeleted>
{
    private readonly ICorpusService _corpusService = corpusService;

    public async Task Consume(ConsumeContext<DataFileDeleted> context)
    {
        await _corpusService.DeleteAllCorpusFilesAsync(context.Message.DataFileId, context.CancellationToken);
    }
}
