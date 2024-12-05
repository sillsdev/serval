namespace Serval.DataFiles.Models;

public record CorpusFile
{
    public required string FileRef { get; init; }
    public string? TextId { get; init; }
}
