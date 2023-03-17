namespace Serval.Translation.Models;

public class WordGraphArc
{
    public int PrevState { get; set; }
    public int NextState { get; set; }
    public float Score { get; set; }
    public List<string> Tokens { get; set; } = new List<string>();
    public List<float> Confidences { get; set; } = new List<float>();
    public int SourceSegmentStart { get; set; }
    public int SourceSegmentEnd { get; set; }
    public List<AlignedWordPair> Alignment { get; set; } = new List<AlignedWordPair>();
    public List<TranslationSources> Sources { get; set; } = new List<TranslationSources>();
}
