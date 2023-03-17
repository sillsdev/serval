namespace Serval.ApiServer;

public class DataFileRetriever : IDataFileRetriever
{
    private readonly IDataFileService _dataFileService;

    public DataFileRetriever(IDataFileService dataFileService)
    {
        _dataFileService = dataFileService;
    }

    public async Task<DataFileResult?> GetDataFileAsync(
        string id,
        string owner,
        CancellationToken cancellationToken = default
    )
    {
        DataFile? dataFile = await _dataFileService.GetAsync(id, owner, cancellationToken);
        if (dataFile is null)
            return null;
        return new DataFileResult
        {
            DataFileId = id,
            Name = dataFile.Name,
            Filename = dataFile.Filename,
            Format = dataFile.Format
        };
    }
}
