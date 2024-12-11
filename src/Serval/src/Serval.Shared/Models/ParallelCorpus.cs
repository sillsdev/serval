namespace Serval.Shared.Models;

public record ParallelCorpus
{
    public required string Id { get; set; }
    public IReadOnlyList<MonolingualCorpus> SourceCorpora { get; set; } = new List<MonolingualCorpus>();
    public IReadOnlyList<MonolingualCorpus> TargetCorpora { get; set; } = new List<MonolingualCorpus>();
}
