namespace Serval.Shared.Contracts;

public record GetDataFile
{
    public string DataFileId { get; init; } = default!;
    public string Owner { get; init; } = default!;
}
