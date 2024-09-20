namespace Serval.Translation.Contracts;

public record ParallelCorpusDto
{
    public required ResourceLinkDto Corpus { get; set; }
    public IReadOnlyList<string> SourceCorporaRefs { get; set; } = new List<string>();
    public IReadOnlyList<string> TargetCorporaRefs { get; set; } = new List<string>();
}
