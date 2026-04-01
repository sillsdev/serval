namespace Serval.Translation.Contracts;

public record TranslationResultContract
{
    public required string Translation { get; set; }
    public required IReadOnlyList<string> SourceTokens { get; set; }
    public required IReadOnlyList<string> TargetTokens { get; set; }
    public required IReadOnlyList<double> Confidences { get; set; }
    public required IReadOnlyList<IReadOnlySet<TranslationSource>> Sources { get; set; }
    public required IReadOnlyList<AlignedWordPairContract> Alignment { get; set; }
    public required IReadOnlyList<PhraseContract> Phrases { get; set; }
}
