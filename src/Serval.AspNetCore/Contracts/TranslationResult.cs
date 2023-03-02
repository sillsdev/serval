namespace Serval.AspNetCore.Contracts;

public record TranslationResult
{
    public string Translation { get; init; } = default!;
    public string[] Tokens { get; init; } = default!;
    public float[] Confidences { get; init; } = default!;
    public TranslationSources[] Sources { get; init; } = default!;
    public AlignedWordPair[] Alignment { get; init; } = default!;
    public Phrase[] Phrases { get; init; } = default!;
}
