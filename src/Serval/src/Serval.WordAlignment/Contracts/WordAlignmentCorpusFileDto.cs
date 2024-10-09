namespace Serval.WordAlignment.Contracts;

public record WordAlignmentCorpusFileDto
{
    public required ResourceLinkDto File { get; init; }
    public string? TextId { get; init; }
}
