namespace Serval.Translation.Models;

public record TranslationResult
{
    public required string Translation { get; set; }
    public required IReadOnlyList<string> SourceTokens { get; set; }
    public required IReadOnlyList<string> TargetTokens { get; set; }
    public required IReadOnlyList<double> Confidences { get; set; }
    public required IReadOnlyList<IReadOnlySet<TranslationSource>> Sources { get; set; }
    public required IReadOnlyList<AlignedWordPair> Alignment { get; set; }
    public required IReadOnlyList<Phrase> Phrases { get; set; }
}
