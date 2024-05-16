namespace Serval.Assessment.Consumers;

public class DataFileDeletedConsumer(ICorpusService corpusService) : IConsumer<DataFileDeleted>
{
    private readonly ICorpusService _corpusService = corpusService;

    public Task Consume(ConsumeContext<DataFileDeleted> context)
    {
        return _corpusService.DataFileDeleted(context.Message.DataFileId, context.CancellationToken);
    }
}
