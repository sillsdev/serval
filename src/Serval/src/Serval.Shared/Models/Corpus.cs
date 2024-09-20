namespace Serval.Shared.Models;

public record Corpus
{
    public string? Name { get; init; }
    public required string Language { get; init; }
    public required IReadOnlyList<CorpusFile> Files { get; init; }
}
