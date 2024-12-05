namespace Serval.Shared.Contracts;

public record WordAlignmentBuildFinished
{
    public required string BuildId { get; init; }
    public required string EngineId { get; init; }
    public required string Owner { get; init; }
    public required JobState BuildState { get; init; }
    public required string Message { get; init; }
    public required DateTime DateFinished { get; init; }
}
