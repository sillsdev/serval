namespace Serval.Shared.Contracts;

public record QueueDto
{
    public required int Size { get; init; }
    public required string EngineType { get; init; }
}
