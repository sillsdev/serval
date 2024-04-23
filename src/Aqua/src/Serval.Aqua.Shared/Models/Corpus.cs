namespace Serval.Aqua.Shared.Models;

public record Corpus : IEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string Language { get; init; }
    public required IReadOnlyList<string> Engines { get; init; }
    public required int DataRevision { get; init; }
    public int? VersionId { get; init; }
    public int? RevisionId { get; init; }
}
