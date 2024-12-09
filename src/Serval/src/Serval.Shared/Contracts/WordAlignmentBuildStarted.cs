namespace Serval.Shared.Contracts;

public record WordAlignmentBuildStarted
{
    public required string BuildId { get; init; }
    public required string EngineId { get; init; }
    public required string Owner { get; init; }
}
