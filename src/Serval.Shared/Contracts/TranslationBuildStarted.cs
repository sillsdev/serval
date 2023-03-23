namespace Serval.Shared.Contracts;

public record TranslationBuildStarted
{
    public string BuildId { get; init; } = default!;
    public string EngineId { get; init; } = default!;
    public string Owner { get; init; } = default!;
}
