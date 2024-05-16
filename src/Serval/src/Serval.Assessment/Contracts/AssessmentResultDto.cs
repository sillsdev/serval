namespace Serval.Assessment.Contracts;

public record AssessmentResultDto
{
    public required string TextId { get; init; }
    public required string Ref { get; init; }
    public double? Score { get; init; }
    public string? Description { get; init; }
}
