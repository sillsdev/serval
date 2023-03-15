using Serval.Corpora.Entities;
using Serval.Shared.Contracts;
using Serval.Shared.Services;
using SIL.DataAccess;

namespace Serval.ApiServer;

public class DataFileRetriever : IDataFileRetriever
{
    private readonly IRepository<DataFile> _dataFiles;

    public DataFileRetriever(IRepository<DataFile> dataFiles)
    {
        _dataFiles = dataFiles;
    }

    public async Task<DataFileResult?> GetDataFileAsync(
        string id,
        string owner,
        CancellationToken cancellationToken = default
    )
    {
        DataFile? dataFile = await _dataFiles.GetAsync(f => f.Id == id && f.Owner == owner, cancellationToken);
        if (dataFile is null)
            return null;
        return new DataFileResult
        {
            DataFileId = id,
            Filename = dataFile.Filename,
            Format = dataFile.Format.ToString()
        };
    }
}
