namespace Serval.Assessment.Consumers;

public class DataFileUpdatedConsumer(ICorpusService corpusService) : IConsumer<DataFileUpdated>
{
    private readonly ICorpusService _corpusService = corpusService;

    public Task Consume(ConsumeContext<DataFileUpdated> context)
    {
        return _corpusService.DataFileUpdated(context.Message.DataFileId, context.CancellationToken);
    }
}
