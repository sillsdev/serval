namespace Serval.Translation.Models;

public class WordGraphArc
{
    public int PrevState { get; set; }
    public int NextState { get; set; }
    public double Score { get; set; }
    public List<string> TargetTokens { get; set; } = default!;
    public List<double> Confidences { get; set; } = default!;
    public int SourceSegmentStart { get; set; }
    public int SourceSegmentEnd { get; set; }
    public List<AlignedWordPair> Alignment { get; set; } = default!;
    public List<IReadOnlySet<TranslationSource>> Sources { get; set; } = default!;
}
