namespace Serval.Translation.Models;

public class TrainingCorpus
{
    public string CorpusRef { get; set; } = default!;
    public IList<string>? TextIds { get; set; }

    public Dictionary<string, List<int>>? Chapters { get; set; }
}
