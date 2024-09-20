namespace Serval.Corpora.Models;

public record Corpus : IOwnedEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string Language { get; init; }
    public string? Name { get; set; }
    public required string Owner { get; init; }
    public required IReadOnlyList<CorpusFile> Files { get; set; }
}
