namespace Serval.Shared.Contracts;

public record TranslationBuildStartedDto
{
    public required ResourceLinkDto Build { get; init; }
    public required ResourceLinkDto Engine { get; init; }
}
