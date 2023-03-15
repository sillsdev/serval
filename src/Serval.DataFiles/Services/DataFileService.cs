using Serval.Shared.Contracts;

namespace Serval.Corpora.Services;

public class DataFileService : EntityServiceBase<DataFile>, IDataFileService
{
    private readonly IOptionsMonitor<DataFileOptions> _options;
    private readonly IDataAccessContext _dataAccessContext;
    private readonly IEventBroker _eventBroker;

    public DataFileService(
        IRepository<DataFile> dataFiles,
        IDataAccessContext dataAccessContext,
        IOptionsMonitor<DataFileOptions> options,
        IEventBroker eventBroker
    )
        : base(dataFiles)
    {
        _dataAccessContext = dataAccessContext;
        _options = options;
        _eventBroker = eventBroker;
    }

    public async Task<IEnumerable<DataFile>> GetAllAsync(string owner, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(c => c.Owner == owner, cancellationToken);
    }

    public async Task CreateAsync(DataFile dataFile, Stream stream, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.BeginTransactionAsync(cancellationToken);
        await Entities.InsertAsync(dataFile, cancellationToken);
        string path = GetDataFilePath(dataFile.Id);
        try
        {
            using FileStream fileStream = File.OpenWrite(path);
            await stream.CopyToAsync(fileStream, cancellationToken);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);
    }

    public override async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.BeginTransactionAsync(cancellationToken);
        DataFile? dataFile = await Entities.DeleteAsync(id, cancellationToken);
        if (dataFile is not null)
        {
            string path = GetDataFilePath(dataFile.Id);
            if (File.Exists(path))
                File.Delete(path);
        }
        await _eventBroker.PublishAsync(new DataFileDeleted { DataFileId = id }, cancellationToken);
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);
        return dataFile is not null;
    }

    private string GetDataFilePath(string id)
    {
        return Path.Combine(_options.CurrentValue.FilesDirectory, id);
    }
}
