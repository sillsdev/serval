namespace Serval.WordAlignment.Dtos;

public record WordAlignmentRequestDto
{
    public required string SourceSegment { get; init; }
    public required string TargetSegment { get; init; }
}
