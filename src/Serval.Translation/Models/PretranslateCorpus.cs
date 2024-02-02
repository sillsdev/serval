namespace Serval.Translation.Models;

public class PretranslateCorpus
{
    public string CorpusRef { get; set; } = default!;
    public List<string>? TextIds { get; set; }
    public Dictionary<string, List<int>>? Chapters { get; set; }
}
