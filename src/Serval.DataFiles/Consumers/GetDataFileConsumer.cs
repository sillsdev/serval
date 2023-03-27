namespace Serval.DataFiles.Consumers;

public class GetDataFileConsumer : IConsumer<GetDataFile>
{
    private readonly IDataFileService _dataFileService;

    public GetDataFileConsumer(IDataFileService dataFileService)
    {
        _dataFileService = dataFileService;
    }

    public async Task Consume(ConsumeContext<GetDataFile> context)
    {
        DataFile? dataFile = await _dataFileService.GetAsync(
            context.Message.DataFileId,
            context.Message.Owner,
            context.CancellationToken
        );
        if (dataFile is null)
        {
            await context.RespondAsync(
                new DataFileNotFound { DataFileId = context.Message.DataFileId, Owner = context.Message.Owner }
            );
        }
        else
        {
            await context.RespondAsync(
                new DataFileResult
                {
                    DataFileId = dataFile.Id,
                    Name = dataFile.Name,
                    Filename = dataFile.Filename,
                    Format = dataFile.Format
                }
            );
        }
    }
}
