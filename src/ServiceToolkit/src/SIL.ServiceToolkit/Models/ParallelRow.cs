namespace SIL.ServiceToolkit.Models;

public record ParallelRow
{
    public required IReadOnlyList<string> SourceRefs { get; init; }
    public required IReadOnlyList<string> TargetRefs { get; init; }
    public required string TargetText { get; init; }
    public required IReadOnlyList<string>? SourceTokens { get; init; }
    public required IReadOnlyList<string>? TargetTokens { get; init; }
    public IReadOnlyList<AlignedWordPair>? Alignment { get; init; }
}
