namespace Serval.Shared.Contracts;

public record AssessmentJobStartedDto
{
    public required ResourceLinkDto Job { get; init; }
    public required ResourceLinkDto Engine { get; init; }
}
