namespace Serval.Shared.Services;

public interface IJobService<TJob>
    where TJob : IJob
{
    Task<IEnumerable<TJob>> GetAllAsync(string engineId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<TJob> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<TJob?> GetActiveAsync(string parentId, CancellationToken cancellationToken = default);
    Task<EntityChange<TJob>> GetNewerRevisionAsync(
        string id,
        long minRevision,
        CancellationToken cancellationToken = default
    );
    Task<EntityChange<TJob>> GetActiveNewerRevisionAsync(
        string parentId,
        long minRevision,
        CancellationToken cancellationToken = default
    );
}
