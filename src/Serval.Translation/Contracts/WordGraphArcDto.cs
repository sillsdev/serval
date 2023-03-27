namespace Serval.Translation.Contracts;

public class WordGraphArcDto
{
    public int PrevState { get; set; }
    public int NextState { get; set; }
    public float Score { get; set; }
    public string[] Tokens { get; set; } = default!;
    public float[] Confidences { get; set; } = default!;
    public int SourceSegmentStart { get; set; }
    public int SourceSegmentEnd { get; set; }
    public AlignedWordPairDto[] Alignment { get; set; } = default!;
    public TranslationSource[][] Sources { get; set; } = default!;
}
