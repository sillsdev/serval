namespace Serval.Translation.Contracts;

public record TranslationCorpusFileConfigDto
{
    public required string FileId { get; init; }

    public string? TextId { get; init; }
}
