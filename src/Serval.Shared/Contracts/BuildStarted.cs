namespace Serval.Shared.Contracts;

public record BuildStarted
{
    public string BuildId { get; init; } = default!;
    public string EngineId { get; init; } = default!;
    public string Owner { get; init; } = default!;
}
