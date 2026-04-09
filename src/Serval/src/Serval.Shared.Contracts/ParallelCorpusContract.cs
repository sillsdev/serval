namespace Serval.Shared.Contracts;

public record ParallelCorpusContract
{
    public required string Id { get; set; }
    public List<MonolingualCorpusContract> SourceCorpora { get; set; } = [];
    public List<MonolingualCorpusContract> TargetCorpora { get; set; } = [];
}
