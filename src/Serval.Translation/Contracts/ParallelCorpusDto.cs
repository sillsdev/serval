namespace Serval.Translation.Contracts;

public class ParallelCorpusDto
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public ResourceLinkDto Engine { get; set; } = default!;
    public string? Name { get; set; }
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    public bool Pretranslate { get; set; }
    public ParallelCorpusFileDto[] SourceFiles { get; set; } = default!;
    public ParallelCorpusFileDto[] TargetFiles { get; set; } = default!;
}
