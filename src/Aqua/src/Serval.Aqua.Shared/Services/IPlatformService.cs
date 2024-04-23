namespace Serval.Aqua.Shared.Services;

public interface IPlatformService
{
    Task JobStartedAsync(string jobId, CancellationToken cancellationToken = default);
    Task JobCompletedAsync(string jobId, CancellationToken cancellationToken = default);
    Task JobCanceledAsync(string jobId, CancellationToken cancellationToken = default);
    Task JobFaultedAsync(string jobId, string message, CancellationToken cancellationToken = default);
    Task JobRestartingAsync(string jobId, CancellationToken cancellationToken = default);

    Task InsertResultsAsync(string jobId, IEnumerable<Result> results, CancellationToken cancellationToken = default);
}
