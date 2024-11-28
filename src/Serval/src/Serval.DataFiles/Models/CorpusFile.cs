namespace Serval.DataFiles.Models;

public record CorpusFile
{
    public required DataFileReference FileReference { get; init; }
    public string? TextId { get; init; }
}
