namespace Serval.Translation.Models;

public class ParallelCorpus
{
    public string Id { get; set; } = default!;
    public string? Name { get; set; }
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    public bool Pretranslate { get; set; }
    public List<ParallelCorpusFile> SourceFiles { get; set; } = new List<ParallelCorpusFile>();
    public List<ParallelCorpusFile> TargetFiles { get; set; } = new List<ParallelCorpusFile>();
}
