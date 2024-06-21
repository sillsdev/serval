namespace Serval.Translation.Models;

public record Phrase
{
    public required int SourceSegmentStart { get; set; }
    public required int SourceSegmentEnd { get; set; }
    public required int TargetSegmentCut { get; set; }
}
