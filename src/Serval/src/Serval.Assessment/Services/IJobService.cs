namespace Serval.Assessment.Services;

public interface IJobService
{
    Task<IEnumerable<Job>> GetAllAsync(string engineId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<Job> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<EntityChange<Job>> GetNewerRevisionAsync(
        string id,
        long minRevision,
        CancellationToken cancellationToken = default
    );
}
