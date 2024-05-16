namespace Serval.Assessment.Services;

public interface IResultService
{
    Task<IEnumerable<Result>> GetAllAsync(
        string engineId,
        string jobId,
        string? textId = null,
        CancellationToken cancellationToken = default
    );
}
