namespace Serval.WordAlignment.Models;

public record Engine : IOwnedEntity, IInitializableEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public string? Name { get; init; }
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public required string Type { get; init; }
    public required string Owner { get; init; }
    public required IReadOnlyList<ParallelCorpus> ParallelCorpora { get; init; }
    public bool IsBuilding { get; init; }
    public int ModelRevision { get; init; }
    public double Confidence { get; init; }
    public int CorpusSize { get; init; }
    public bool? IsInitialized { get; set; }
    public DateTime? DateCreated { get; set; }
}
