namespace Serval.WordAlignment.Contracts;

public record WordAlignmentCorpusFileConfigDto
{
    public required string FileId { get; init; }

    public string? TextId { get; init; }
}
