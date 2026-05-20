namespace Serval.Machine.Shared.Models;

public record Pretranslation
{
    public required string CorpusId { get; init; }
    public required string TextId { get; init; }
    public required IReadOnlyList<string> SourceRefs { get; init; }
    public required IReadOnlyList<string> TargetRefs { get; init; }
    public required string Translation { get; init; }
    public required IEnumerable<string> SourceTokens { get; init; }
    public required IEnumerable<string> TranslationTokens { get; init; }
    public required IReadOnlyList<AlignedWordPair> Alignment { get; init; }
    public double Confidence { get; init; }
}
