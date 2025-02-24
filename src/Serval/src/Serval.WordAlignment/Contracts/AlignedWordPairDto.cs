namespace Serval.WordAlignment.Contracts;

public record AlignedWordPairDto
{
    public required int SourceIndex { get; init; }
    public required int TargetIndex { get; init; }
    public required double Score { get; init; }
}
