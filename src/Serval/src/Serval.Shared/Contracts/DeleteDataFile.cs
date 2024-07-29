namespace Serval.Shared.Contracts;

public record DeleteDataFile
{
    public required string DataFileId { get; init; }
}
