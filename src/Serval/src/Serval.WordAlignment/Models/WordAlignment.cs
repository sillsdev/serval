namespace Serval.WordAlignment.Models;

public class WordAlignment : IEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string EngineRef { get; init; }
    public int ModelRevision { get; init; }
    public required string CorpusRef { get; init; }
    public required string TextId { get; init; }
    public required IReadOnlyList<string> Refs { get; init; }
    public required IReadOnlyList<string> SourceTokens { get; set; }
    public required IReadOnlyList<string> TargetTokens { get; set; }
    public required IReadOnlyList<AlignedWordPair> Alignment { get; set; }
}
