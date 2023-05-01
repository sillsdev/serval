namespace Serval.Translation.Models;

public class PretranslateCorpus
{
    public string CorpusRef { get; set; } = default!;
    public IList<string>? TextIds { get; set; }
}
