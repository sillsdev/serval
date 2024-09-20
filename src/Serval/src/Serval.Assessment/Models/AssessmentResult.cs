namespace Serval.Assessment.Models;

public record AssessmentResult : IBuildResult
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string EngineRef { get; init; }
    public int BuildRevision { get; init; }
    public required string TextId { get; init; }
    public required string Ref { get; init; }
    public double? Score { get; init; }
    public string? Description { get; init; }
}
