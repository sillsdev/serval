namespace Serval.DataFiles.Services;

public interface IDataFileService
{
    Task<IEnumerable<DataFile>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<DataFile> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<DataFile> GetAsync(string id, string owner, CancellationToken cancellationToken = default);
    Task CreateAsync(DataFile dataFile, Stream stream, CancellationToken cancellationToken = default);
    Task<Stream> ReadAsync(string id, CancellationToken cancellationToken = default);
    Task<DataFile> UpdateAsync(string id, Stream stream, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
