namespace Serval.Shared.Consumers;

public class DataFileDeletedConsumer<TEngineService>(TEngineService engineService) : IConsumer<DataFileDeleted>
    where TEngineService : IEngineServiceBase
{
    private readonly TEngineService _engineService = engineService;

    public async Task Consume(ConsumeContext<DataFileDeleted> context)
    {
        await _engineService.RemoveDataFileFromAllCorporaAsync(context.Message.DataFileId, context.CancellationToken);
    }
}
