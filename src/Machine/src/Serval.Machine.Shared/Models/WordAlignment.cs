namespace Serval.Machine.Shared.Models;

public record WordAlignment
{
    public required string CorpusId { get; init; }
    public required string TextId { get; init; }
    public required IReadOnlyList<string> Refs { get; init; }
    public required IReadOnlyList<string> SourceTokens { get; set; }
    public required IReadOnlyList<string> TargetTokens { get; set; }
    public required IReadOnlyList<AlignedWordPair> Alignment { get; set; }
}
