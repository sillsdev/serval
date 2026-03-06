namespace Serval.Shared.Contracts;

public record AlignedWordPairDto
{
    public required int SourceIndex { get; init; }
    public required int TargetIndex { get; init; }
    public double? Score { get; init; }
}
