namespace Serval.Shared.Contracts;

public record AssessmentJobStarted
{
    public required string JobId { get; init; }
    public required string EngineId { get; init; }
    public required string Owner { get; init; }
}
