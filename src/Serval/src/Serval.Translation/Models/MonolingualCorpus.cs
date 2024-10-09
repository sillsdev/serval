namespace Serval.Translation.Models;

public record MonolingualCorpus
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public required string Language { get; set; }
    public required IReadOnlyList<CorpusFile> Files { get; set; }
}
