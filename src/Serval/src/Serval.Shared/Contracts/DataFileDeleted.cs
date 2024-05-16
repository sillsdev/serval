namespace Serval.Shared.Contracts;

public record DataFileDeleted
{
    public required string DataFileId { get; init; }
}
