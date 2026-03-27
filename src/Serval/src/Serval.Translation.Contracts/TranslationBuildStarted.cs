namespace Serval.Translation.Contracts;

public record TranslationBuildStarted(string BuildId, string EngineId, string Owner) : IEvent;
