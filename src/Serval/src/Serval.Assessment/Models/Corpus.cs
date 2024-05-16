namespace Serval.Assessment.Models;

public record Corpus : IOwnedEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string Owner { get; init; }
    public string? Name { get; init; }
    public required string Language { get; init; }
    public required IReadOnlyList<CorpusFile> Files { get; init; }
    public int DataRevision { get; init; } = 1;
}
