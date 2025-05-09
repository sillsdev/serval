namespace Serval.Shared.Models;

public record ParallelCorpusFilter
{
    public required string CorpusRef { get; set; }
    public IReadOnlyList<string>? TextIds { get; set; }
    public string? ScriptureRange { get; set; }
}
