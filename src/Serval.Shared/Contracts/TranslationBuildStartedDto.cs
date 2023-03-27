namespace Serval.Shared.Contracts;

public class TranslationBuildStartedDto
{
    public ResourceLinkDto Build { get; set; } = default!;
    public ResourceLinkDto Engine { get; set; } = default!;
}
