namespace Serval.Translation.Contracts;

public record TranslationBuildFinished(
    string BuildId,
    string EngineId,
    string Owner,
    JobState BuildState,
    string Message,
    DateTime DateFinished
) : IEvent;
