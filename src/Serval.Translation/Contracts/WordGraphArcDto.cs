namespace Serval.Translation.Contracts;

public class WordGraphArcDto
{
    public int PrevState { get; set; }
    public int NextState { get; set; }
    public float Score { get; set; }
    public List<string> Words { get; set; } = default!;
    public List<float> Confidences { get; set; } = default!;
    public int SourceSegmentStart { get; set; }
    public int SourceSegmentEnd { get; set; }
    public List<AlignedWordPairDto> Alignment { get; set; } = default!;
    public List<List<TranslationSource>> Sources { get; set; } = default!;
}
