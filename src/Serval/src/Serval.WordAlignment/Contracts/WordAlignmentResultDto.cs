namespace Serval.WordAlignment.Contracts;

public record WordAlignmentResultDto
{
    public required IReadOnlyList<string> SourceTokens { get; init; }
    public required IReadOnlyList<string> TargetTokens { get; init; }
    public required IReadOnlyList<AlignedWordPairDto> Alignment { get; init; }
}
