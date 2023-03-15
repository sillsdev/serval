namespace Serval.Shared.Services;

public interface IDataFileRetriever
{
    Task<DataFileResult?> GetDataFileAsync(string id, string owner, CancellationToken cancellationToken = default);
}
