namespace Serval.Translation.Models;

public class Pretranslation : IEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string EngineRef { get; init; }
    public int ModelRevision { get; init; }
    public required string CorpusRef { get; init; }
    public required string TextId { get; init; }
    public required IReadOnlyList<string> SourceRefs { get; init; }
    public required IReadOnlyList<string> TargetRefs { get; init; }
    public required IReadOnlyList<string> Refs { get; init; }
    public required string Translation { get; init; }
    public IEnumerable<string>? SourceTokens { get; init; }
    public IEnumerable<string>? TranslationTokens { get; init; }
    public IReadOnlyList<AlignedWordPair>? Alignment { get; init; }
}
