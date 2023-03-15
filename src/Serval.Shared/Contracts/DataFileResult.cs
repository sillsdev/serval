namespace Serval.Shared.Contracts;

public record DataFileResult
{
    public string DataFileId { get; init; } = default!;
    public string Filename { get; init; } = default!;
    public string Format { get; init; } = default!;
}
