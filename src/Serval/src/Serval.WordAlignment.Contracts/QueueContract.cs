namespace Serval.WordAlignment.Contracts;

public record QueueContract
{
    public required int Size { get; init; }
    public required string EngineType { get; init; }
}
