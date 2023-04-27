namespace Serval.Translation.Models;

public class WordGraphArc
{
    public int PrevState { get; set; }
    public int NextState { get; set; }
    public double Score { get; set; }
    public List<string> Words { get; set; } = new List<string>();
    public List<double> Confidences { get; set; } = new List<double>();
    public int SourceSegmentStart { get; set; }
    public int SourceSegmentEnd { get; set; }
    public List<AlignedWordPair> Alignment { get; set; } = new List<AlignedWordPair>();
    public List<List<TranslationSource>> Sources { get; set; } = new List<List<TranslationSource>>();
}
