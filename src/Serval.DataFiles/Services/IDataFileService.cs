namespace Serval.Corpora.Services;

public interface IDataFileService
{
    Task<IEnumerable<DataFile>> GetAllAsync(string owner);
    Task<DataFile?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task CreateAsync(DataFile dataFile, Stream stream);
    Task<bool> DeleteAsync(string id);
}
