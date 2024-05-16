namespace Serval.Shared.Contracts;

public record GetDataFile
{
    public required string DataFileId { get; init; }
    public required string Owner { get; init; }
}
