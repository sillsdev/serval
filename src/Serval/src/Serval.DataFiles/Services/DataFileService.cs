using SIL.Machine.Corpora;

namespace Serval.DataFiles.Services;

public class DataFileService : OwnedEntityServiceBase<DataFile>, IDataFileService
{
    private readonly IOptionsMonitor<DataFileOptions> _options;
    private readonly IDataAccessContext _dataAccessContext;
    private readonly IEventRouter _eventRouter;
    private readonly IRepository<DeletedFile> _deletedFiles;
    private readonly IFileSystem _fileSystem;

    public DataFileService(
        IRepository<DataFile> dataFiles,
        IDataAccessContext dataAccessContext,
        IOptionsMonitor<DataFileOptions> options,
        IEventRouter eventRouter,
        IRepository<DeletedFile> deletedFiles,
        IFileSystem fileSystem
    )
        : base(dataFiles)
    {
        _dataAccessContext = dataAccessContext;
        _options = options;
        _eventRouter = eventRouter;
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

    public async Task CreateAsync(DataFile dataFile, Stream stream, CancellationToken cancellationToken = default)
    {
        string filename = Path.GetRandomFileName();
        string path = GetDataFilePath(filename);
        try
        {
            using (Stream fileStream = _fileSystem.OpenWrite(path))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }
            if (dataFile.Format == FileFormat.Paratext)
            {
                ParatextMetadata metadata = await ParseParatextMetadataAsync(path);
                dataFile = dataFile with { FileMetadata = metadata };
            }
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
            await using (Stream fileStream = _fileSystem.OpenWrite(path))
                await stream.CopyToAsync(fileStream, cancellationToken);
            await _dataAccessContext.WithTransactionAsync(
                async ct =>
                {
                    DataFile? originalDataFile = await Entities.UpdateAsync(
                        id,
                        u => u.Set(f => f.Filename, filename),
                        returnOriginal: true,
                        cancellationToken: ct
                    );
                    if (originalDataFile is null)
                        throw new EntityNotFoundException($"Could not find the DataFile '{id}'.");

                    if (originalDataFile.Format == FileFormat.Paratext)
                    {
                        ParatextMetadata metadata = await ParseParatextMetadataAsync(path);
                        await Entities.UpdateAsync(
                            id,
                            u => u.Set(f => f.FileMetadata, metadata),
                            cancellationToken: ct
                        );
                    }

                    await _deletedFiles.InsertAsync(
                        new DeletedFile { Filename = originalDataFile.Filename, DeletedAt = DateTime.UtcNow },
                        cancellationToken: ct
                    );
                    await _eventRouter.PublishAsync(new DataFileUpdated(id, filename), ct);
                },
                cancellationToken: cancellationToken
            );
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

        return await GetAsync(id, cancellationToken);
    }

    public override async Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        await _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                DataFile? dataFile = await Entities.DeleteAsync(id, ct);
                if (dataFile is null)
                    throw new EntityNotFoundException($"Could not find the DataFile '{id}'.");
                // We are intentionally not deleting files so they can be deleted later by DataFileCleaner
                await _deletedFiles.InsertAsync(
                    new DeletedFile { Filename = dataFile.Filename, DeletedAt = DateTime.UtcNow },
                    ct
                );

                await _eventRouter.PublishAsync(new DataFileDeleted(id), ct);
            },
            cancellationToken: cancellationToken
        );

    private string GetDataFilePath(string filename) => Path.Combine(_options.CurrentValue.FilesDirectory, filename);

    private static async Task<ParatextMetadata> ParseParatextMetadataAsync(string path)
    {
        using ZipContainer zipContainer = new(path);
        try
        {
            ParatextProjectSettings projectSettings = new Shared.Services.ZipParatextProjectSettingsParser(
                zipContainer
            ).Parse();
            return new ParatextMetadata
            {
                ProjectGuid = projectSettings.Guid,
                Name = projectSettings.Name,
                LanguageCode = projectSettings.LanguageCode,
                Versification = projectSettings.Versification.Name,
                TranslationType = projectSettings.TranslationType,
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Unable to parse the Paratext project settings for the uploaded data file.",
                e
            );
        }
    }
}
