namespace Serval.DataFiles.Messages;

public record CorpusResult
{
    public required string CorpusId { get; init; }
    public required string Language { get; init; }
    public string? Name { get; init; }
    public required IReadOnlyList<CorpusFileResult> Files { get; set; }
}
