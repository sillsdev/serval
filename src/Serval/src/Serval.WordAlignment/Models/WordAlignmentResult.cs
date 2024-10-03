namespace Serval.WordAlignment.Models;

public record WordAlignmentResult
{
    public required IReadOnlyList<string> SourceTokens { get; set; }
    public required IReadOnlyList<string> TargetTokens { get; set; }
    public required IReadOnlyList<double> Confidences { get; set; }
    public required IReadOnlyList<AlignedWordPair> Alignment { get; set; }
}
