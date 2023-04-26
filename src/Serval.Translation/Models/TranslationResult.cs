namespace Serval.Translation.Models;

public class TranslationResult
{
    public string Translation { get; set; } = default!;
    public List<string> Tokens { get; set; } = new List<string>();
    public List<double> Confidences { get; set; } = new List<double>();
    public List<List<TranslationSource>> Sources { get; set; } = new List<List<TranslationSource>>();
    public List<AlignedWordPair> Alignment { get; set; } = new List<AlignedWordPair>();
    public List<Phrase> Phrases { get; set; } = new List<Phrase>();
}
