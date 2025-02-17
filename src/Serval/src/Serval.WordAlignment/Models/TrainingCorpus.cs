namespace Serval.WordAlignment.Models;

public record TrainingCorpus
{
    public required string ParallelCorpusRef { get; set; }
    public IReadOnlyList<ParallelCorpusFilter>? SourceFilters { get; set; }
    public IReadOnlyList<ParallelCorpusFilter>? TargetFilters { get; set; }
}
