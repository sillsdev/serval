namespace Serval.Translation.Contracts;

public class TranslationBuildDto
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public int Revision { get; set; }
    public ResourceLinkDto Engine { get; set; } = default!;
    public IList<PretranslateCorpusDto>? Pretranslate { get; set; }
    public int Step { get; set; }
    public double? PercentCompleted { get; set; }
    public string? Message { get; set; }

    /// <summary>
    /// The current build job state.
    /// </summary>
    public JobState State { get; set; }
    public DateTime? DateFinished { get; set; }
}
