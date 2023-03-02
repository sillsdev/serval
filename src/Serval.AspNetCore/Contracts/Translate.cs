namespace Serval.AspNetCore.Contracts;

public record Translate
{
    public string EngineId { get; init; } = default!;
    public string Owner { get; init; } = default!;
    public int N { get; init; }
    public string Segment { get; init; } = default!;
}
