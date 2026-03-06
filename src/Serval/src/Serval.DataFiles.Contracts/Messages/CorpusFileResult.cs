namespace Serval.DataFiles.Messages;

public record CorpusFileResult
{
    public required DataFileResult File { get; init; }
    public required string TextId { get; init; }
}
