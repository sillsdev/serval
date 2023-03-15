namespace Serval.Shared.Contracts;

public record BuildFinished
{
    public string BuildId { get; init; } = default!;
    public string EngineId { get; init; } = default!;
    public string Owner { get; init; } = default!;
    public BuildState BuildState { get; init; }
    public string Message { get; init; } = default!;
    public DateTime DateFinished { get; init; }
}
