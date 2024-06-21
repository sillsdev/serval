namespace Serval.Translation.Contracts;

public record TranslationCorpusConfigDto
{
    /// <summary>
    /// The corpus name.
    /// </summary>
    public string? Name { get; init; }

    public required string SourceLanguage { get; init; }

    public required string TargetLanguage { get; init; }

    public required IReadOnlyList<TranslationCorpusFileConfigDto> SourceFiles { get; init; }

    public required IReadOnlyList<TranslationCorpusFileConfigDto> TargetFiles { get; init; }
}
