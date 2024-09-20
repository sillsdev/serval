namespace Serval.Shared.Models;

public record FilteredCorpus
{
    public required string CorpusRef { get; set; } = default!;
    public IReadOnlyList<string>? TextIds { get; set; }
    public string? ScriptureRange { get; set; }
}
