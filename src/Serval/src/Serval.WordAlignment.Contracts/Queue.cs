namespace Serval.WordAlignment.Contracts;

public record Queue
{
    public required int Size { get; init; }
    public required string EngineType { get; init; }
}
