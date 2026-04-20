namespace Serval.Translation.Services;

public interface IBuildService
{
    Task<IEnumerable<Build>> GetAllAsync(string owner, CancellationToken cancellationToken = default);
    Task<IEnumerable<Build>> GetAllCreatedAfterAsync(
        string owner,
        DateTime? createdAfter,
        CancellationToken cancellationToken = default
    );
    Task<EntityChange<Build>> GetNextFinishedBuildAsync(
        string owner,
        DateTime finishedAfter,
        CancellationToken cancellationToken = default
    );
}
