namespace Serval.Shared.Contracts;

public record BuildStartedDto
{
    public required ResourceLinkDto Build { get; init; }
    public required ResourceLinkDto Engine { get; init; }
}
