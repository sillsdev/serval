namespace Serval.Shared.Contracts;

public class BuildFinishedDto
{
    public ResourceLinkDto Build { get; set; } = default!;
    public ResourceLinkDto Engine { get; set; } = default!;
    public BuildState BuildState { get; set; }
    public DateTime DateFinished { get; set; }
}
