namespace Serval.DataFiles.Services;

public class DataFileService : EntityServiceBase<DataFile>, IDataFileService
{
    private readonly IOptionsMonitor<DataFileOptions> _options;
    private readonly IDataAccessContext _dataAccessContext;
    private readonly IScopedMediator _mediator;

    public DataFileService(
        IRepository<DataFile> dataFiles,
        IDataAccessContext dataAccessContext,
        IOptionsMonitor<DataFileOptions> options,
        IScopedMediator mediator
    )
        : base(dataFiles)
    {
        _dataAccessContext = dataAccessContext;
        _options = options;
        _mediator = mediator;
        Directory.CreateDirectory(_options.CurrentValue.FilesDirectory);
    }

    public Task<DataFile?> GetAsync(string id, string owner, CancellationToken cancellationToken = default)
    {
        return Entities.GetAsync(f => f.Id == id && f.Owner == owner, cancellationToken);
    }

    public async Task<IEnumerable<DataFile>> GetAllAsync(string owner, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(c => c.Owner == owner, cancellationToken);
    }

    public async Task CreateAsync(DataFile dataFile, Stream stream, CancellationToken cancellationToken = default)
    {
        dataFile.Filename = Path.GetRandomFileName();
        await _dataAccessContext.BeginTransactionAsync(cancellationToken);
        await Entities.InsertAsync(dataFile, cancellationToken);
        string path = GetDataFilePath(dataFile);
        try
        {
            using FileStream fileStream = File.OpenWrite(path);
            await stream.CopyToAsync(fileStream, cancellationToken);
        }
        catch
        {
            if (File.Exists(path))
                File.Delete(path);
            throw;
        }
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);
    }

    public override async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.BeginTransactionAsync(cancellationToken);
        DataFile? dataFile = await Entities.DeleteAsync(id, cancellationToken);
        if (dataFile is not null)
        {
            string path = GetDataFilePath(dataFile);
            if (File.Exists(path))
                File.Delete(path);
        }
        await _mediator.Publish(new DataFileDeleted { DataFileId = id }, cancellationToken);
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);
        return dataFile is not null;
    }

    private string GetDataFilePath(DataFile dataFile)
    {
        return Path.Combine(_options.CurrentValue.FilesDirectory, dataFile.Filename);
    }
}
