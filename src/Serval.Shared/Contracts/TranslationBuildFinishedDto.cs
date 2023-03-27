namespace Serval.Shared.Contracts;

public class TranslationBuildFinishedDto
{
    public ResourceLinkDto Build { get; set; } = default!;
    public ResourceLinkDto Engine { get; set; } = default!;
    public JobState BuildState { get; set; }
    public DateTime DateFinished { get; set; }
}
