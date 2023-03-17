namespace Serval.Translation.Contracts;

public class CorpusDto
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public ResourceLinkDto Engine { get; set; } = default!;
    public string? Name { get; set; }
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    public bool Pretranslate { get; set; }
    public CorpusFileDto[] SourceFiles { get; set; } = default!;
    public CorpusFileDto[] TargetFiles { get; set; } = default!;
}
