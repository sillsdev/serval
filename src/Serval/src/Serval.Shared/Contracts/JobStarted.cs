namespace Serval.Shared.Contracts;

public record JobStarted
{
    public required string BuildId { get; init; }
    public required string EngineId { get; init; }
    public required string Owner { get; init; }
    public required string Type { get; init; }
}
