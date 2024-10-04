namespace Serval.Shared.Contracts;

public record JobFinishedDto
{
    public required ResourceLinkDto Job { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public required JobState JobState { get; init; }
    public required string Message { get; init; }
    public required DateTime DateFinished { get; init; }
}
