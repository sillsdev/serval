namespace Serval.Translation.Contracts;

public record PretranslationContract
{
    public required string CorpusId { get; init; }
    public required string TextId { get; init; }
    public required IReadOnlyList<string> SourceRefs { get; init; }
    public required IReadOnlyList<string> TargetRefs { get; init; }
    public required string Translation { get; init; }
    public IReadOnlyList<string>? SourceTokens { get; init; }
    public IReadOnlyList<string>? TranslationTokens { get; init; }
    public IReadOnlyList<AlignedWordPairContract>? Alignment { get; init; }
    public double? Confidence { get; init; }
}
