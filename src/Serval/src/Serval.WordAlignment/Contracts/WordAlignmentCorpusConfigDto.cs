namespace Serval.WordAlignment.Contracts;

public record WordAlignmentCorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; init; }

    public required string SourceLanguage { get; init; }

    public required string TargetLanguage { get; init; }

    public required IReadOnlyList<WordAlignmentCorpusFileConfigDto> SourceFiles { get; init; }

    public required IReadOnlyList<WordAlignmentCorpusFileConfigDto> TargetFiles { get; init; }
}
