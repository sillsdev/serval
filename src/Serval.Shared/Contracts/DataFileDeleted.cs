namespace Serval.Shared.Contracts;

public record DataFileDeleted
{
    public string DataFileId { get; init; } = default!;
}
