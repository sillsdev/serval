namespace Serval.WordAlignment.Models;

public record WordAlignmentResult
{
    public required IReadOnlyList<string> SourceTokens { get; init; }
    public required IReadOnlyList<string> TargetTokens { get; init; }
    public required IReadOnlyList<AlignedWordPair> Alignment { get; init; }
}
