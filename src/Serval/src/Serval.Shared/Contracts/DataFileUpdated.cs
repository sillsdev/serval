namespace Serval.Shared.Contracts;

public record DataFileUpdated
{
    public required string DataFileId { get; init; }
    public required string Filename { get; init; }
}
