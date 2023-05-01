namespace Serval.Translation.Models;

public class WordGraphArc
{
    public int PrevState { get; set; }
    public int NextState { get; set; }
    public double Score { get; set; }
    public IList<string> TargetTokens { get; set; } = default!;
    public IList<double> Confidences { get; set; } = default!;
    public int SourceSegmentStart { get; set; }
    public int SourceSegmentEnd { get; set; }
    public IList<AlignedWordPair> Alignment { get; set; } = default!;
    public IList<IReadOnlySet<TranslationSource>> Sources { get; set; } = default!;
}
