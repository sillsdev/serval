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

    public async Task<DataFile> GetAsync(string id, string owner, CancellationToken cancellationToken = default)
    {
        DataFile? dataFile = await Entities.GetAsync(f => f.Id == id && f.Owner == owner, cancellationToken);
        if (dataFile is null)
            throw new EntityNotFoundException($"Could not find the DataFile '{id}' with owner '{owner}'.");
        return dataFile;
    }

    public async Task<IEnumerable<DataFile>> GetAllAsync(string owner, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(c => c.Owner == owner, cancellationToken);
    }

    public async Task CreateAsync(DataFile dataFile, Stream stream, CancellationToken cancellationToken = default)
    {
        string filename = Path.GetRandomFileName();
        string path = GetDataFilePath(filename);
        try
        {
            using Stream fileStream = _fileSystem.OpenWrite(path);
            await stream.CopyToAsync(fileStream, cancellationToken);
            await Entities.InsertAsync(dataFile with { Filename = filename }, cancellationToken);
        }
        catch
        {
            _fileSystem.DeleteFile(path);
            throw;
        }
    }

    public async Task<Stream> ReadAsync(string id, CancellationToken cancellationToken = default)
    {
        DataFile? dataFile = await GetAsync(id, cancellationToken);
        if (dataFile is null)
            throw new EntityNotFoundException($"Could not find the DataFile '{id}'.");
        string path = GetDataFilePath(dataFile.Filename);
        return _fileSystem.OpenRead(path);
    }

    public async Task<DataFile> UpdateAsync(string id, Stream stream, CancellationToken cancellationToken = default)
    {
        string filename = Path.GetRandomFileName();
        string path = GetDataFilePath(filename);
        bool deleteFile = false;
        try
        {
            using (Stream fileStream = _fileSystem.OpenWrite(path))
                await stream.CopyToAsync(fileStream, cancellationToken);
            await _dataAccessContext.WithTransactionAsync(
                async (ct) =>
                {
                    DataFile? originalDataFile = await Entities.UpdateAsync(
                        id,
                        u => u.Set(f => f.Filename, filename),
                        returnOriginal: true,
                        cancellationToken: ct
                    );
                    if (originalDataFile is null)
                    {
                        throw new EntityNotFoundException($"Could not find the DataFile '{id}'.");
                    }
                    else
                    {
                        await _deletedFiles.InsertAsync(
                            new DeletedFile { Filename = originalDataFile.Filename, DeletedAt = DateTime.UtcNow },
                            ct
                        );
                    }
                },
                cancellationToken: cancellationToken
            );
            return await GetAsync(id, cancellationToken);
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

    public override async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                DataFile? dataFile = await Entities.DeleteAsync(id, cancellationToken);
                if (dataFile is null)
                    throw new EntityNotFoundException($"Could not find the DataFile '{id}'.");
                await _deletedFiles.InsertAsync(
                    new DeletedFile { Filename = dataFile.Filename, DeletedAt = DateTime.UtcNow },
                    cancellationToken
                );
                await _mediator.Publish(new DataFileDeleted { DataFileId = id }, cancellationToken);
            },
            cancellationToken: cancellationToken
        );
    }

    private string GetDataFilePath(string filename)
    {
        return Path.Combine(_options.CurrentValue.FilesDirectory, filename);
    }
}
