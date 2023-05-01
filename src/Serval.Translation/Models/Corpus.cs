namespace Serval.Translation.Models;

public class Corpus
{
    public string Id { get; set; } = default!;
    public string? Name { get; set; }
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    public IList<CorpusFile> SourceFiles { get; set; } = default!;
    public IList<CorpusFile> TargetFiles { get; set; } = default!;
}
