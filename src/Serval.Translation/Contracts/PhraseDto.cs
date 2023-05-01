namespace Serval.Translation.Contracts;

public class PhraseDto
{
    public int SourceSegmentStart { get; set; }
    public int SourceSegmentEnd { get; set; }
    public int TargetSegmentCut { get; set; }
    public double Confidence { get; set; }
}
