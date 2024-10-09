namespace Serval.Translation.Models;

public record TrainingCorpus
{
    public string? CorpusRef { get; set; } = default!;
    public IReadOnlyList<string>? TextIds { get; set; }
    public string? ScriptureRange { get; set; }

    public string? ParallelCorpusRef { get; set; }
    public IReadOnlyList<ParallelCorpusFilter>? SourceFilters { get; set; }
    public IReadOnlyList<ParallelCorpusFilter>? TargetFilters { get; set; }
}
