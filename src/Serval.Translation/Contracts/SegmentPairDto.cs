namespace Serval.Translation.Contracts;

public record SegmentPairDto
{
    public required string SourceSegment { get; init; }
    public required string TargetSegment { get; init; }
    public required bool SentenceStart { get; init; }
}
