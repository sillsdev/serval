namespace Serval.Translation.Models;

public record ParallelCorpus
{
    public required string Id { get; set; }
    public IReadOnlyList<ParallelCorpusSubcorpus> SourceCorpora { get; set; } = new List<ParallelCorpusSubcorpus>();
    public IReadOnlyList<ParallelCorpusSubcorpus> TargetCorpora { get; set; } = new List<ParallelCorpusSubcorpus>();
}
