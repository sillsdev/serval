namespace Serval.Shared.Contracts;

public record BuildFinishedDto
{
    public required ResourceLinkDto Build { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public required BuildState BuildState { get; init; }
    public required string Message { get; init; }
    public required DateTime DateFinished { get; init; }
}
