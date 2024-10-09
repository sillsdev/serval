namespace Serval.Translation.Models;

public record PretranslateCorpus
{
    public string? CorpusRef { get; set; }
    public IReadOnlyList<string>? TextIds { get; set; }
    public string? ScriptureRange { get; set; }

    public string? ParallelCorpusRef { get; set; }
    public IReadOnlyList<ParallelCorpusFilter>? SourceFilters { get; set; }
}
