namespace Serval.Shared.Models;

public record FilteredParallelCorpus
{
    public required string Id { get; set; }
    public IReadOnlyList<FilteredMonolingualCorpus> SourceCorpora { get; set; } = [];
    public IReadOnlyList<FilteredMonolingualCorpus> TargetCorpora { get; set; } = [];
}
