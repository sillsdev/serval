namespace Serval.Translation.Models;

public class Phrase
{
    public int SourceSegmentStart { get; set; }
    public int SourceSegmentEnd { get; set; }
    public int TargetSegmentCut { get; set; }
}
