namespace Serval.Machine.Shared.Models;

public record WordAlignment
{
    public required string CorpusId { get; init; }
    public required string TextId { get; init; }
    public required IReadOnlyList<string> Refs { get; init; }
    public required IReadOnlyList<string> SourceTokens { get; set; }
    public required IReadOnlyList<string> TargetTokens { get; set; }
    public required IReadOnlyList<double> Confidences { get; set; } //TODO It seems to me that it'd more natural to have the confidence as part of the word pair object - but I understand that this is currently not the case with the translation result; would it be breaking to change it there too?
    public required IReadOnlyList<AlignedWordPair> Alignment { get; set; }
}
