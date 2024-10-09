namespace Serval.WordAlignment.Models;

public record WordAlignmentCorpus
{
    public required string CorpusRef { get; set; }
    public IReadOnlyList<string>? TextIds { get; set; }
    public string? ScriptureRange { get; set; }
}
