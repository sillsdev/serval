namespace Serval.Shared.Contracts;

public record DataFileResult
{
    public string DataFileId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Filename { get; init; } = default!;
    public FileFormat Format { get; init; } = default!;
}
