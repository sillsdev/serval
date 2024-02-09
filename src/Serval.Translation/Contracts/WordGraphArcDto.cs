namespace Serval.Translation.Contracts;

public record WordGraphArcDto
{
    public required int PrevState { get; init; }
    public required int NextState { get; init; }
    public required double Score { get; init; }
    public required IReadOnlyList<string> TargetTokens { get; init; }
    public required IReadOnlyList<double> Confidences { get; init; }
    public required int SourceSegmentStart { get; init; }
    public required int SourceSegmentEnd { get; init; }
    public required IReadOnlyList<AlignedWordPairDto> Alignment { get; init; }
    public required IReadOnlyList<IReadOnlySet<TranslationSource>> Sources { get; init; }
}
