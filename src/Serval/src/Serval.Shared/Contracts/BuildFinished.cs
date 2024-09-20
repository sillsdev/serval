namespace Serval.Shared.Contracts;

public record BuildFinished
{
    public required string BuildId { get; init; }
    public required string EngineId { get; init; }
    public required string Owner { get; init; }
    public required string Type { get; init; }
    public required BuildState BuildState { get; init; }
    public required string Message { get; init; }
    public required DateTime DateFinished { get; init; }
}
