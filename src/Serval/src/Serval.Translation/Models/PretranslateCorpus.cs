namespace Serval.Translation.Models;

public record PretranslateCorpus
{
    public required string CorpusRef { get; set; }
    public IReadOnlyList<string>? TextIds { get; set; }
    public string? ScriptureRange { get; set; }
}
