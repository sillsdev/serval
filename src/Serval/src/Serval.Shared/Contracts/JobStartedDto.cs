namespace Serval.Shared.Contracts;

public record JobStartedDto
{
    public required ResourceLinkDto Job { get; init; }
    public required ResourceLinkDto Engine { get; init; }
}
