namespace Serval.Shared.Dtos;

public record QueueDto
{
    public required int Size { get; init; }
    public required string EngineType { get; init; }
}
