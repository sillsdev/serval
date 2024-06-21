namespace Serval.Translation.Contracts;

public record TranslationResultDto
{
    public required string Translation { get; init; }
    public required IReadOnlyList<string> SourceTokens { get; init; }
    public required IReadOnlyList<string> TargetTokens { get; init; }
    public required IReadOnlyList<double> Confidences { get; init; }
    public required IReadOnlyList<IReadOnlySet<TranslationSource>> Sources { get; init; }
    public required IReadOnlyList<AlignedWordPairDto> Alignment { get; init; }
    public required IReadOnlyList<PhraseDto> Phrases { get; init; }
}
