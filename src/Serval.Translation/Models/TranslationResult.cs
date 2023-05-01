namespace Serval.Translation.Models;

public class TranslationResult
{
    public string Translation { get; set; } = default!;
    public List<string> SourceTokens { get; set; } = default!;
    public List<string> TargetTokens { get; set; } = default!;
    public List<double> Confidences { get; set; } = default!;
    public List<IReadOnlySet<TranslationSource>> Sources { get; set; } = default!;
    public List<AlignedWordPair> Alignment { get; set; } = default!;
    public List<Phrase> Phrases { get; set; } = default!;
}
