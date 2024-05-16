namespace Serval.Shared.Contracts;

public record AssessmentJobFinishedDto
{
    public required ResourceLinkDto Job { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public required JobState JobState { get; init; }
    public required DateTime DateFinished { get; init; }
}
