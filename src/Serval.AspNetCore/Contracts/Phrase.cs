namespace Serval.AspNetCore.Contracts;

public record Phrase
{
    public int SourceSegmentStart { get; init; }
    public int SourceSegmentEnd { get; init; }
    public int TargetSegmentCut { get; init; }
    public double Confidence { get; init; }
}
