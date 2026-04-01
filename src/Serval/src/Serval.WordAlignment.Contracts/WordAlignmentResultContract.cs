namespace Serval.WordAlignment.Contracts;

public record WordAlignmentResultContract
{
    public required IReadOnlyList<string> SourceTokens { get; init; }
    public required IReadOnlyList<string> TargetTokens { get; init; }
    public required IReadOnlyList<AlignedWordPairContract> Alignment { get; init; }
}
