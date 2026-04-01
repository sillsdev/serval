namespace Serval.Translation.Contracts;

public record PhraseContract
{
    public required int SourceSegmentStart { get; set; }
    public required int SourceSegmentEnd { get; set; }
    public required int TargetSegmentCut { get; set; }
}
