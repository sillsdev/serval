namespace Serval.Translation.Contracts;

public record TranslationMonolingualCorpusDto
{
    public required string Id { get; set; }
    public string? Name { get; init; }
    public required string Language { get; set; }
    public required IReadOnlyList<TranslationCorpusFileDto> Files { get; set; }
}
