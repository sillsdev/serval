namespace Serval.DataFiles.Services;

public class DataFileDeleter(
    IRepository<DataFile> dataFiles,
    IRepository<DeletedFile> deletedFiles,
    IRepository<Corpus> corpora,
    IDataAccessContext dataAccessContext,
    IEventRouter eventRouter
)
{
    public Task DeleteAsync(string dataFileId, CancellationToken cancellationToken)
    {
        return dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                DataFile? dataFile = await dataFiles.DeleteAsync(dataFileId, ct);
                if (dataFile is null)
                    throw new EntityNotFoundException($"Could not find the DataFile '{dataFileId}'.");
                await deletedFiles.InsertAsync(
                    new DeletedFile { Filename = dataFile.Filename, DeletedAt = DateTime.UtcNow },
                    ct
                );
                await corpora.UpdateAllAsync(
                    e => e.Files.Any(f => f.FileRef == dataFileId),
                    u => u.RemoveAll(e => e.Files, f => f.FileRef == dataFileId),
                    cancellationToken: ct
                );
                await eventRouter.PublishAsync(new DataFileDeleted(dataFileId), ct);
            },
            cancellationToken
        );
    }
}
