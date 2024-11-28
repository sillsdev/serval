namespace Serval.Shared.Contracts;

public record CorpusResult
{
    public required string CorpusId { get; init; }
    public required string Language { get; init; }
    public string? Name { get; init; }
    public required IReadOnlyList<CorpusFile> Files { get; set; }
}
