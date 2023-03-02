namespace Serval.AspNetCore.Contracts;

public record AlignedWordPair
{
    public int SourceIndex { get; init; }
    public int TargetIndex { get; init; }
}
