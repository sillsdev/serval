namespace Serval.Translation.Contracts;

public class TranslationResultDto
{
    public string Translation { get; set; } = default!;
    public IList<string> SourceTokens { get; set; } = default!;
    public IList<string> TargetTokens { get; set; } = default!;
    public IList<float> Confidences { get; set; } = default!;
    public IList<IReadOnlySet<TranslationSource>> Sources { get; set; } = default!;
    public IList<AlignedWordPairDto> Alignment { get; set; } = default!;
    public IList<PhraseDto> Phrases { get; set; } = default!;
}
