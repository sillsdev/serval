namespace Serval.DataFiles.Contracts;

public record CorpusFileDto
{
    public required DataFileReferenceDto File { get; init; }
    public string? TextId { get; init; }
}
