namespace Serval.DataFiles.Messages;

public record DataFileUpdated
{
    public required string DataFileId { get; init; }
    public required string Filename { get; init; }
}
