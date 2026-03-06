namespace Serval.DataFiles.Messages;

public record DataFileDeleted
{
    public required string DataFileId { get; init; }
}
