namespace Serval.Translation.Contracts;

public class TranslationResultDto
{
    public string Translation { get; set; } = default!;
    public string[] Tokens { get; set; } = default!;
    public float[] Confidences { get; set; } = default!;
    public TranslationSource[][] Sources { get; set; } = default!;
    public AlignedWordPairDto[] Alignment { get; set; } = default!;
    public PhraseDto[] Phrases { get; set; } = default!;
}
