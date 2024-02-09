namespace Serval.Shared.Contracts;

public record TranslationBuildStarted
{
    public required string BuildId { get; init; }
    public required string EngineId { get; init; }
    public required string Owner { get; init; }
}
