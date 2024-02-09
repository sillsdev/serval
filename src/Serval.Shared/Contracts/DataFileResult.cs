namespace Serval.Shared.Contracts;

public record DataFileResult
{
    public required string DataFileId { get; init; }
    public required string Name { get; init; }
    public required string Filename { get; init; }
    public required FileFormat Format { get; init; }
}
