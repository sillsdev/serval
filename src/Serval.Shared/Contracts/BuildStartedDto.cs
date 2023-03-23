namespace Serval.Shared.Contracts;

public class BuildStartedDto
{
    public ResourceLinkDto Build { get; set; } = default!;
    public ResourceLinkDto Engine { get; set; } = default!;
}
