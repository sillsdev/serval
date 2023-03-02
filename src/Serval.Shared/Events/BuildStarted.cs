namespace Serval.Shared.Events;

public record BuildStarted
{
    public string BuildId { get; init; } = default!;
    public string EngineId { get; init; } = default!;
    public string Owner { get; init; } = default!;
}
