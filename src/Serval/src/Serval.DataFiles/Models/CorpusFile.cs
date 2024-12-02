namespace Serval.DataFiles.Models;

public record CorpusFile
{
    public required string FileId { get; init; }
    public string? TextId { get; init; }
}
