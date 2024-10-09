namespace Serval.DataFiles.Contracts;

public record CorpusFileDto
{
    public required DataFileDto File { get; init; }
    public string? TextId { get; init; }
}
