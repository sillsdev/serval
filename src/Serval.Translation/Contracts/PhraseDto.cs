namespace Serval.Translation.Contracts;

public record PhraseDto
{
    public required int SourceSegmentStart { get; init; }
    public required int SourceSegmentEnd { get; init; }
    public required int TargetSegmentCut { get; init; }
}
