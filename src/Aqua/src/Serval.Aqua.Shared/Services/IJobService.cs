namespace Serval.Aqua.Shared.Services;

public interface IJobService
{
    Task<Job?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task CreateAsync(
        string engineId,
        string id,
        AssessmentType assessmentType,
        JobData data,
        string? options,
        CorpusFilter corpusFilter,
        CancellationToken cancellationToken = default
    );

    Task<bool> StartAssessmentStageAsync(
        string engineId,
        string id,
        AssessmentType assessmentType,
        int revisionId,
        int? referenceId = null,
        string? options = null,
        CancellationToken cancellationToken = default
    );

    Task<bool> StartPostprocessStageAsync(
        string engineId,
        string id,
        AssessmentType assessmentType,
        int assessmentId,
        string? options = null,
        CancellationToken cancellationToken = default
    );

    Task CancelAsync(string id, CancellationToken cancellationToken = default);

    Task<string?> GetStageIdAsync(string id, CancellationToken cancellationToken = default);

    Task<bool?> IsCancelingAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> GetActiveJobsAsync(JobStage stage, CancellationToken cancellationToken = default);

    Task<bool> StageStartedAsync(string id, CancellationToken cancellationToken = default);

    Task StageFinishedAsync(string id, CancellationToken cancellationToken = default);

    Task StageRestartingAsync(string id, CancellationToken cancellationToken = default);
}
