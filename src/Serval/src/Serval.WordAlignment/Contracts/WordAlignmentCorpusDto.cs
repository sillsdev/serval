namespace Serval.WordAlignment.Contracts;

public record WordAlignmentCorpusDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public string? Name { get; init; }
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public required IReadOnlyList<WordAlignmentCorpusFileDto> SourceFiles { get; init; }
    public required IReadOnlyList<WordAlignmentCorpusFileDto> TargetFiles { get; init; }
}
