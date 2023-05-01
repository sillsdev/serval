namespace Serval.Translation.Contracts;

public class TranslationCorpusDto
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public ResourceLinkDto Engine { get; set; } = default!;
    public string? Name { get; set; }
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    public IList<TranslationCorpusFileDto> SourceFiles { get; set; } = default!;
    public IList<TranslationCorpusFileDto> TargetFiles { get; set; } = default!;
}
