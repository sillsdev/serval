namespace Serval.Translation.Contracts;

public record TranslationBuildStarted : IEvent
{
    public required string BuildId { get; init; }
    public required string EngineId { get; init; }
    public required string Owner { get; init; }
}
