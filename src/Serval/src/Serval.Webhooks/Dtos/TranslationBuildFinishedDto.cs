namespace Serval.Shared.Contracts;

public record TranslationBuildFinishedDto
{
    public required ResourceLinkDto Build { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public required JobState BuildState { get; init; }
    public required string Message { get; init; }
    public required DateTime DateFinished { get; init; }
}
