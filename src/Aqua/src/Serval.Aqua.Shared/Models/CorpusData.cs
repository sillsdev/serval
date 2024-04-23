namespace Serval.Aqua.Shared.Models;

public record CorpusData
{
    public required string Id { get; init; }
    public required string Language { get; init; }
    public required int DataRevision { get; init; }
    public required IReadOnlyList<CorpusFile> Files { get; init; }
}
