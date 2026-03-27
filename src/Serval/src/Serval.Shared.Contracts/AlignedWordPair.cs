namespace Serval.Shared.Contracts;

public record AlignedWordPair
{
    public required int SourceIndex { get; set; }
    public required int TargetIndex { get; set; }
    public double Score { get; set; }
}
