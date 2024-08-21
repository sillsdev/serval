namespace Serval.Assessment.Models;

public record Engine : IOwnedEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string Owner { get; init; }
    public string? Name { get; init; }
    public required string Type { get; init; }
    public required Corpus Corpus { get; init; }
    public Corpus? ReferenceCorpus { get; init; }
}
