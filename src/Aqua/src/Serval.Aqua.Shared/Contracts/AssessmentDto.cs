namespace Serval.Aqua.Shared.Contracts;

public record AssessmentDto
{
    public required int Id { get; init; }
    public required int RevisionId { get; init; }
    public required int? ReferenceId { get; init; }
    public required AssessmentType Type { get; init; }
    public AssessmentStatus? Status { get; init; }
    public DateTime? RequestedTime { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
}
