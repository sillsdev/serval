namespace Serval.Aqua.Shared.Services;

public interface IAquaService
{
    Task<VersionDto> CreateVersionAsync(
        string name,
        string language,
        string abbreviation,
        CancellationToken cancellationToken = default
    );

    Task DeleteVersionAsync(int versionId, CancellationToken cancellationToken = default);

    Task<RevisionDto> CreateRevisionAsync(
        int versionId,
        string fileName,
        CancellationToken cancellationToken = default
    );

    Task DeleteRevisionAsync(int revisionId, CancellationToken cancellationToken = default);

    Task<AssessmentDto> CreateAssessmentAsync(
        AssessmentType type,
        int revisionId,
        int? referenceId = null,
        CancellationToken cancellationToken = default
    );

    Task DeleteAssessmentAsync(int assessmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssessmentDto>> GetAssessmentsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResultDto>> GetResultsAsync(
        int assessmentId,
        string? book = null,
        int? chapter = null,
        CancellationToken cancellationToken = default
    );
}
