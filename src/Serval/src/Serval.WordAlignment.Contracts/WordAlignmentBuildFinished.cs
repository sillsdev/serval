namespace Serval.WordAlignment.Contracts;

public record WordAlignmentBuildFinished(
    string BuildId,
    string EngineId,
    string Owner,
    JobState BuildState,
    string Message,
    DateTime DateFinished
) : IEvent;
