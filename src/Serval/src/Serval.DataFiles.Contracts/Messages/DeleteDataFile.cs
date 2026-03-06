namespace Serval.DataFiles.Messages;

public record DeleteDataFile
{
    public required string DataFileId { get; init; }
}
