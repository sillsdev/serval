namespace Serval.Translation.Contracts;

public class TranslationResultDto
{
    public string Translation { get; set; } = default!;
    public List<string> Tokens { get; set; } = default!;
    public List<float> Confidences { get; set; } = default!;
    public List<List<TranslationSource>> Sources { get; set; } = default!;
    public List<AlignedWordPairDto> Alignment { get; set; } = default!;
    public List<PhraseDto> Phrases { get; set; } = default!;
}
