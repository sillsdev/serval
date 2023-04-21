namespace Serval.Translation.Services;

public interface IBuildService
{
    Task<IEnumerable<Build>> GetAllAsync(string parentId, CancellationToken cancellationToken = default);
    Task<Build?> GetAsync(string id, CancellationToken cancellationToken = default);
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
    Task<bool> IsBuildActive(string parentId, CancellationToken cancellationToken = default);
}
