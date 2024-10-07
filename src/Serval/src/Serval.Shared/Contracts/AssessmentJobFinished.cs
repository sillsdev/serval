namespace Serval.Shared.Contracts;

public record AssessmentJobFinished
{
    public required string JobId { get; init; }
    public required string EngineId { get; init; }
    public required string Owner { get; init; }
    public required BuildState JobState { get; init; }
    public required string Message { get; init; }
    public required DateTime DateFinished { get; init; }
}
