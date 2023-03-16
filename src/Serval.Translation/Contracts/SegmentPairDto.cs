namespace Serval.Translation.Contracts;

public class SegmentPairDto
{
    public string SourceSegment { get; set; } = default!;
    public string TargetSegment { get; set; } = default!;
    public bool SentenceStart { get; set; }
}
