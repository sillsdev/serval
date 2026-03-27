namespace Serval.WordAlignment.Contracts;

public record WordAlignmentBuildStarted(string BuildId, string EngineId, string Owner) : IEvent;
