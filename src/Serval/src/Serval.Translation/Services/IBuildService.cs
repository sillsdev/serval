namespace Serval.Translation.Services;

public interface IBuildService
{
    Task<IEnumerable<Build>> GetAllAsync(string parentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Build>> GetAllAsync(
        string ownerId,
        string parentId,
        CancellationToken cancellationToken = default
    );
    Task<IEnumerable<Build>> GetAllCreatedAfterAsync(
        string owner,
        DateTime? createdAfter,
        CancellationToken cancellationToken = default
    );
    Task<Build> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<Build?> GetActiveAsync(string parentId, CancellationToken cancellationToken = default);
    Task<EntityChange<Build>> GetNewerRevisionAsync(
        string id,
        long minRevision,
        CancellationToken cancellationToken = default
    );
    Task<EntityChange<Build>> GetActiveNewerRevisionAsync(
        string parentId,
        long minRevision,
        CancellationToken cancellationToken = default
    );
    Task<EntityChange<Build>> GetNextCompletedBuildAsync(
        string owner,
        DateTime finishedAfter,
        CancellationToken cancellationToken = default
    );
}
