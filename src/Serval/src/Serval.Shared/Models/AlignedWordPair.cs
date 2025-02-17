namespace Serval.Shared.Models;

public record AlignedWordPair
{
    public required int SourceIndex { get; set; }
    public required int TargetIndex { get; set; }
}
