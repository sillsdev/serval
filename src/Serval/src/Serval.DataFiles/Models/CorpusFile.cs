namespace Serval.DataFiles.Models;

public record CorpusFile
{
    public required DataFile File { get; init; }
    public string? TextId { get; init; }
}
