namespace Serval.Translation.Models;

public class Corpus
{
    public string Id { get; set; } = default!;
    public string? Name { get; set; }
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    public List<CorpusFile> SourceFiles { get; set; } = new List<CorpusFile>();
    public List<CorpusFile> TargetFiles { get; set; } = new List<CorpusFile>();
}
