namespace Serval.Shared.Contracts;

public record DataFileNotFound
{
    public required string DataFileId { get; init; }
    public required string Owner { get; init; }
}
