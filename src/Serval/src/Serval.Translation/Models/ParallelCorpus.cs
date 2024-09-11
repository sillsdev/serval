namespace Serval.Translation.Models;

public record ParallelCorpus
{
    public required string Id { get; set; }
    public IReadOnlyList<string> SourceCorporaRefs { get; set; } = new List<string>();
    public IReadOnlyList<string> TargetCorporaRefs { get; set; } = new List<string>();
}
