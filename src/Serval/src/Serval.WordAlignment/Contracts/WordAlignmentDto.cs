namespace Serval.WordAlignment.Contracts;

public record WordAlignmentDto
{
    public required string TextId { get; init; }
    public required IReadOnlyList<string> SourceRefs { get; init; }
    public required IReadOnlyList<string> TargetRefs { get; init; }

    [Obsolete]
    public IReadOnlyList<string>? Refs { get; init; }
    public required IReadOnlyList<string> SourceTokens { get; init; }
    public required IReadOnlyList<string> TargetTokens { get; init; }
    public required IReadOnlyList<AlignedWordPairDto> Alignment { get; init; }
}
