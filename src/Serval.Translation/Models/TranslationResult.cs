namespace Serval.Translation.Models;

public class TranslationResult
{
    public string Translation { get; set; } = default!;
    public List<string> Tokens { get; set; } = new List<string>();
    public List<float> Confidences { get; set; } = new List<float>();
    public List<TranslationSources> Sources { get; set; } = new List<TranslationSources>();
    public List<AlignedWordPair> Alignment { get; set; } = new List<AlignedWordPair>();
    public List<Phrase> Phrases { get; set; } = new List<Phrase>();
}
