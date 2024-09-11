namespace Serval.Translation.Models;

public record TrainingCorpus
{
    public string? CorpusRef { get; set; } = default!;
    public IReadOnlyList<string>? TextIds { get; set; }
    public string? ScriptureRange { get; set; }

    public string? ParallelCorpusRef { get; set; }
    public ParallelCorpusFilter? SourceFilter { get; set; }
    public ParallelCorpusFilter? TargetFilter { get; set; }
}
