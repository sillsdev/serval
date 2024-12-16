namespace Serval.WordAlignment.Models;

public record WordAlignmentCorpus
{
    public string? ParallelCorpusRef { get; set; }
    public IReadOnlyList<ParallelCorpusFilter>? SourceFilters { get; set; }
    public IReadOnlyList<ParallelCorpusFilter>? TargetFilters { get; set; }
}
