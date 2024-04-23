namespace Serval.Aqua.Shared.Contracts;

public record ResultDto
{
    public int? Id { get; init; }
    public int? AssessmentId { get; init; }
    public string? Vref { get; init; }
    public string? Source { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, string>>? Target { get; init; }
    public required double Score { get; init; }
    public required bool Flag { get; init; }
    public string? Note { get; init; }
    public string? RevisionText { get; init; }
    public string? ReferenceText { get; init; }
    public required bool Hide { get; init; }
}
