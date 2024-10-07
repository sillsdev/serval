namespace Serval.Assessment.Models;

public record AssessmentBuild : IBuild
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public string? Name { get; init; }
    public required string EngineRef { get; init; }
    public int? QueueDepth { get; init; }
    public IReadOnlyList<string>? TextIds { get; set; }
    public string? ScriptureRange { get; set; }
    public double? PercentCompleted { get; init; }
    public string? Message { get; init; }
    public BuildState State { get; init; } = BuildState.Pending;
    public DateTime? DateFinished { get; init; }
    public IReadOnlyDictionary<string, object>? Options { get; init; }
}
