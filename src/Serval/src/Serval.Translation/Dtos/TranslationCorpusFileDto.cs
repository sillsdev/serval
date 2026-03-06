namespace Serval.Translation.Dtos;

public record TranslationCorpusFileDto
{
    public required ResourceLinkDto File { get; init; }
    public string? TextId { get; init; }
}
