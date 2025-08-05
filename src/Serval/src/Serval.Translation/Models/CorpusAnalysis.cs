namespace Serval.Shared.Models;

public record CorpusAnalysis
{
    public required string CorpusRef { get; init; }
    public required string SourceQuoteConvention { get; init; }
    public required string TargetQuoteConvention { get; init; }
}
