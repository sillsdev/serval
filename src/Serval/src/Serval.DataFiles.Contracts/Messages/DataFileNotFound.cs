namespace Serval.DataFiles.Messages;

public record DataFileNotFound
{
    public required string DataFileId { get; init; }
    public required string Owner { get; init; }
}
