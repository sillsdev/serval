namespace Serval.DataFiles.Messages;

public record GetDataFile
{
    public required string DataFileId { get; init; }
    public required string Owner { get; init; }
}
