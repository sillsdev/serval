namespace Serval.Corpora.Services;

public class DataFileService : EntityServiceBase<DataFile>, IDataFileService
{
    private readonly IOptionsMonitor<DataFileOptions> _options;
    private readonly IDataAccessContext _dataAccessContext;

    public DataFileService(
        IRepository<DataFile> dataFiles,
        IDataAccessContext dataAccessContext,
        IOptionsMonitor<DataFileOptions> options
    )
        : base(dataFiles)
    {
        _dataAccessContext = dataAccessContext;
        _options = options;
    }

    public async Task<IEnumerable<DataFile>> GetAllAsync(string owner)
    {
        return await Entities.GetAllAsync(c => c.Owner == owner);
    }

    public async Task CreateAsync(DataFile dataFile, Stream stream)
    {
        await _dataAccessContext.BeginTransactionAsync();
        await Entities.InsertAsync(dataFile);
        string path = GetDataFilePath(dataFile.Id);
        using FileStream fileStream = File.Create(path);
        await stream.CopyToAsync(fileStream);
        await _dataAccessContext.CommitTransactionAsync();
    }

    public override async Task<bool> DeleteAsync(string id)
    {
        await _dataAccessContext.BeginTransactionAsync();
        DataFile? dataFile = await Entities.DeleteAsync(id);
        if (dataFile is not null)
        {
            string path = GetDataFilePath(dataFile.Id);
            if (File.Exists(path))
                File.Delete(path);
        }
        await _dataAccessContext.CommitTransactionAsync();
        return dataFile is not null;
    }

    private string GetDataFilePath(string id)
    {
        return Path.Combine(_options.CurrentValue.FilesDirectory, id);
    }
}
