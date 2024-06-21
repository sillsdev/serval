namespace Serval.Translation.Models;

public record WordGraphArc
{
    public required int PrevState { get; set; }
    public required int NextState { get; set; }
    public required double Score { get; set; }
    public required IReadOnlyList<string> TargetTokens { get; set; }
    public required IReadOnlyList<double> Confidences { get; set; }
    public required int SourceSegmentStart { get; set; }
    public required int SourceSegmentEnd { get; set; }
    public required IReadOnlyList<AlignedWordPair> Alignment { get; set; }
    public required IReadOnlyList<IReadOnlySet<TranslationSource>> Sources { get; set; }
}
