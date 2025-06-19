namespace Serval.Machine.Shared.Models;

public record Pretranslation
{
    public required string CorpusId { get; init; }
    public required string TextId { get; init; }
    public required IReadOnlyList<string> Refs { get; init; }
    public required string Translation { get; init; }
    public IEnumerable<string>? SourceTokens { get; init; }
    public IEnumerable<string>? TranslationTokens { get; init; }
    public IReadOnlyList<AlignedWordPair>? Alignment { get; init; }
}
