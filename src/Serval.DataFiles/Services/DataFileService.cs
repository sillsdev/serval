namespace Serval.DataFiles.Services;

public class DataFileService : EntityServiceBase<DataFile>, IDataFileService
{
    private readonly IOptionsMonitor<DataFileOptions> _options;
    private readonly IDataAccessContext _dataAccessContext;
    private readonly IScopedMediator _mediator;
    private readonly IRepository<DeletedFile> _deletedFiles;
    private readonly IFileSystem _fileSystem;

    public DataFileService(
        IRepository<DataFile> dataFiles,
        IDataAccessContext dataAccessContext,
        IOptionsMonitor<DataFileOptions> options,
        IScopedMediator mediator,
        IRepository<DeletedFile> deletedFiles,
        IFileSystem fileSystem
    )
        : base(dataFiles)
    {
        _dataAccessContext = dataAccessContext;
        _options = options;
        _mediator = mediator;
        _deletedFiles = deletedFiles;
        _fileSystem = fileSystem;
        _fileSystem.CreateDirectory(_options.CurrentValue.FilesDirectory);
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
        string path = GetDataFilePath(dataFile.Filename);
        try
        {
            using Stream fileStream = _fileSystem.OpenWrite(path);
            await stream.CopyToAsync(fileStream, cancellationToken);
            await Entities.InsertAsync(dataFile, cancellationToken);
        }
        catch
        {
            _fileSystem.DeleteFile(path);
            throw;
        }
    }

    public async Task<DataFile?> UpdateAsync(string id, Stream stream, CancellationToken cancellationToken = default)
    {
        string filename = Path.GetRandomFileName();
        string path = GetDataFilePath(filename);
        bool deleteFile = false;
        try
        {
            using (Stream fileStream = _fileSystem.OpenWrite(path))
                await stream.CopyToAsync(fileStream, cancellationToken);

            await _dataAccessContext.BeginTransactionAsync(cancellationToken);
            DataFile? originalDataFile = await Entities.UpdateAsync(
                id,
                u => u.Set(f => f.Filename, filename),
                returnOriginal: true,
                cancellationToken: cancellationToken
            );
            if (originalDataFile is null)
            {
                deleteFile = true;
            }
            else
            {
                await _deletedFiles.InsertAsync(
                    new DeletedFile { Filename = originalDataFile.Filename, DeletedAt = DateTime.UtcNow },
                    cancellationToken
                );
            }
            await _dataAccessContext.CommitTransactionAsync(cancellationToken);
            return originalDataFile is null ? null : await GetAsync(id, cancellationToken);
        }
        catch
        {
            deleteFile = true;
            throw;
        }
        finally
        {
            if (deleteFile)
                _fileSystem.DeleteFile(path);
        }
    }

    public override async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        // return true if the deletion was successful, false if the file did not exist or was already deleted.
        await _dataAccessContext.BeginTransactionAsync(cancellationToken);
        DataFile? dataFile = await Entities.DeleteAsync(id, cancellationToken);
        if (dataFile is not null)
        {
            await _deletedFiles.InsertAsync(
                new DeletedFile { Filename = dataFile.Filename, DeletedAt = DateTime.UtcNow },
                cancellationToken
            );
        }
        await _mediator.Publish(new DataFileDeleted { DataFileId = id }, cancellationToken);
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);
        return dataFile is not null;
    }

    private string GetDataFilePath(string filename)
    {
        return Path.Combine(_options.CurrentValue.FilesDirectory, filename);
    }
}
