namespace Serval.Shared.Contracts;

public record DataFileNotFound
{
    public string DataFileId { get; init; } = default!;
    public string Owner { get; init; } = default!;
}
