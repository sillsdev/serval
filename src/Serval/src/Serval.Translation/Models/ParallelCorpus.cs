namespace Serval.Translation.Models;

public record ParallelCorpus
{
    public required string Id { get; set; }
    public required IReadOnlyList<MonolingualCorpus> SourceCorpora { get; set; }
    public required IReadOnlyList<MonolingualCorpus> TargetCorpora { get; set; }
}
