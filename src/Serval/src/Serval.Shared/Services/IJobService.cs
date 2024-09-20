namespace Serval.Shared.Services;

public interface IBuildService<TBuild>
    where TBuild : IBuild
{
    Task<IEnumerable<TBuild>> GetAllAsync(string engineId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<TBuild> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<TBuild?> GetActiveAsync(string parentId, CancellationToken cancellationToken = default);
    Task<EntityChange<TBuild>> GetNewerRevisionAsync(
        string id,
        long minRevision,
        CancellationToken cancellationToken = default
    );
    Task<EntityChange<TBuild>> GetActiveNewerRevisionAsync(
        string parentId,
        long minRevision,
        CancellationToken cancellationToken = default
    );
}
