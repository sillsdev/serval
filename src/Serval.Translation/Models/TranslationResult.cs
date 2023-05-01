namespace Serval.Translation.Models;

public class TranslationResult
{
    public string Translation { get; set; } = default!;
    public IList<string> SourceTokens { get; set; } = new List<string>();
    public IList<string> TargetTokens { get; set; } = new List<string>();
    public IList<double> Confidences { get; set; } = new List<double>();
    public IList<IReadOnlySet<TranslationSource>> Sources { get; set; } = default!;
    public IList<AlignedWordPair> Alignment { get; set; } = default!;
    public IList<Phrase> Phrases { get; set; } = new List<Phrase>();
}
