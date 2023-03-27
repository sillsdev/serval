namespace Serval.Translation.Contracts;

public class TranslationCorpusFileDto
{
    public ResourceLinkDto File { get; set; } = default!;
    public string? TextId { get; set; }
}
