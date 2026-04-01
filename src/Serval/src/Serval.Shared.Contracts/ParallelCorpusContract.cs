namespace Serval.Shared.Contracts;

public record ParallelCorpusContract
{
    public required string Id { get; set; }
    public IReadOnlyList<MonolingualCorpusContract> SourceCorpora { get; set; } = [];
    public IReadOnlyList<MonolingualCorpusContract> TargetCorpora { get; set; } = [];
}
