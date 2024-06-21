namespace Serval.Translation.Contracts;

public record TranslationCorpusDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required ResourceLinkDto Engine { get; init; }
    public string? Name { get; init; }
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public required IReadOnlyList<TranslationCorpusFileDto> SourceFiles { get; init; }
    public required IReadOnlyList<TranslationCorpusFileDto> TargetFiles { get; init; }
}
